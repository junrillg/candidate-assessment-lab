using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CandidateAssessment.Assessments;

internal sealed class AssessmentMaintenance(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AssessmentMaintenance> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions MessageJson = new(JsonSerializerDefaults.Web);
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        LoopAsync(ExpireAsync, TimeSpan.FromSeconds(1), "expiry", stoppingToken),
        LoopAsync(PublishAsync, TimeSpan.FromSeconds(1), "outbox", stoppingToken),
        LoopAsync(CleanupAsync, TimeSpan.FromSeconds(30), "cleanup", stoppingToken));

    private async Task LoopAsync(Func<CancellationToken, Task> work, TimeSpan interval, string operation, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await work(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning("Assessments {Operation} will retry ({ErrorType})", operation, exception.GetType().Name); }
            try { await Task.Delay(interval, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        }
    }
    private async Task ExpireAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDb>();
        var ids = await db.Attempts.FromSqlRaw("""SELECT * FROM assessments.attempts WHERE "SubmittedAt" IS NULL AND "DeletedAt" IS NULL AND "Deadline" <= clock_timestamp() ORDER BY "Deadline" LIMIT 50""")
            .AsNoTracking().Select(x => x.Id).ToArrayAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var attempt = await AssessmentModule.LockAsync(db, id, cancellationToken);
            if (attempt is { SubmittedAt: null, DeletedAt: null } && await AssessmentModule.Now(db, cancellationToken) >= attempt.Deadline)
                await AssessmentModule.FinalizeAsync(attempt, db, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }
    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDb>();
        var messages = await db.Outbox.AsNoTracking().Where(x => x.PublishedAt == null)
            .OrderBy(x => x.Id).Take(12).ToListAsync(cancellationToken);
        if (messages.Count == 0) return;
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:19098",
            EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = 5000,
            RequestTimeoutMs = 3000, SocketTimeoutMs = 3000
        }).SetLogHandler((_, _) => { }).Build();
        foreach (var message in messages)
        {
            var payload = new ProcessingIntent(message.Id, message.AttemptId, message.QuestionId, message.CorrelationId);
            await producer.ProduceAsync(configuration["Kafka:Topic"] ?? "assessment.processing.v1",
                new Message<string, string> { Key = message.AttemptId.ToString(), Value = JsonSerializer.Serialize(payload, MessageJson) }, cancellationToken);
            // A crash before this mark repeats publication; Evaluation deduplicates the durable intent ID.
            await db.Outbox.Where(x => x.Id == message.Id && x.PublishedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.PublishedAt, DateTimeOffset.UtcNow), cancellationToken);
        }
    }
    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDb>();
        var storage = scope.ServiceProvider.GetRequiredService<RecordingStorage>();
        var ids = await db.Attempts.AsNoTracking().Where(x => x.DeletedAt != null && x.CleanedAt == null)
            .Select(x => x.Id).Take(50).ToArrayAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var attempt = await AssessmentModule.LockAsync(db, id, cancellationToken);
            if (attempt is { DeletedAt: not null, CleanedAt: null })
            {
                var recordings = await db.Recordings.Where(x => x.AttemptId == id).ToListAsync(cancellationToken);
                foreach (var recording in recordings) storage.Delete(recording.Path);
                await db.Answers.Where(x => x.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
                await db.Recordings.Where(x => x.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
                await db.Outbox.Where(x => x.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
                await db.Submissions.Where(x => x.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
                attempt.QuestionsJson = "[]";
                attempt.Title = "";
                attempt.CandidateId = Guid.Empty;
                attempt.CleanedAt = await AssessmentModule.Now(db, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var expired = await db.Recordings.Where(x => x.PurgedAt == null && x.CreatedAt < cutoff).Take(100).ToListAsync(cancellationToken);
        foreach (var recording in expired)
        {
            storage.Delete(recording.Path);
            recording.PurgedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        var paths = (await db.Recordings.AsNoTracking().Where(x => x.PurgedAt == null).Select(x => x.Path).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        await storage.RemoveOrphansAsync(paths, cancellationToken);
    }
}

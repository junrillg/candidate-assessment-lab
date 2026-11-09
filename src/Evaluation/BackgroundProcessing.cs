using System.Text.Json;
using CandidateAssessment.Assessments;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CandidateAssessment.Evaluation;

internal sealed class IntentConsumer(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<IntentConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
                {
                    BootstrapServers = configuration["Kafka:BootstrapServers"], GroupId = "assessment-evaluation-v1",
                    EnableAutoCommit = false, EnableAutoOffsetStore = false, AutoOffsetReset = AutoOffsetReset.Earliest,
                    MaxPollIntervalMs = 300000, SessionTimeoutMs = 10000
                }).SetErrorHandler((_, e) => logger.LogWarning("Kafka consumer state: {Code}", e.Code)).Build();
                consumer.Subscribe(configuration["Kafka:Topic"] ?? "assessment.processing.v1");
                while (!stoppingToken.IsCancellationRequested)
                {
                    var message = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (message is null) continue;
                    ProcessingIntent? intent;
                    try
                    {
                        if (message.Message.Value is null || message.Message.Value.Length > 2048) throw new JsonException();
                        intent = JsonSerializer.Deserialize<ProcessingIntent>(message.Message.Value, EvaluationJson.Options);
                        if (intent is null || intent.Id == Guid.Empty || intent.AttemptId == Guid.Empty
                            || intent.QuestionId == Guid.Empty || intent.CorrelationId == Guid.Empty) throw new JsonException();
                    }
                    catch (JsonException)
                    {
                        logger.LogWarning("Rejected malformed processing metadata at partition {Partition}, offset {Offset}",
                            message.Partition.Value, message.Offset.Value);
                        consumer.Commit(message);
                        continue;
                    }
                    using var scope = scopes.CreateScope();
                    var reader = scope.ServiceProvider.GetRequiredService<IAssessmentReader>();
                    var evidence = await reader.GetEvidenceAsync(intent.Id, stoppingToken);
                    if (evidence is not null && evidence.Intent == intent)
                    {
                        var db = scope.ServiceProvider.GetRequiredService<EvaluationDb>();
                        var now = DateTimeOffset.UtcNow;
                        await db.Database.ExecuteSqlInterpolatedAsync($"""
                            INSERT INTO evaluation.jobs ("Id","AttemptId","QuestionId","CorrelationId","Status",
                            "Failures","Fence","NextRunAt","CreatedAt")
                            VALUES ({intent.Id},{intent.AttemptId},{intent.QuestionId},{intent.CorrelationId},'queued',0,0,{now},{now})
                            ON CONFLICT DO NOTHING
                            """, stoppingToken);
                        logger.LogInformation("Durable evaluation intake {IntentId} for attempt {AttemptId}", intent.Id, intent.AttemptId);
                    }
                    consumer.Commit(message);
                }
                consumer.Close();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Evaluation intake paused ({FailureType}); delivery remains unacknowledged", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}

internal sealed class EvaluationWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<EvaluationWorker> logger) : BackgroundService
{
    private const int LeaseSeconds = 300;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Clamp(configuration.GetValue("Evaluation:Concurrency", 2), 1, 4);
        return Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => RunAsync(stoppingToken)));
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EvaluationDb>();
                var reader = scope.ServiceProvider.GetRequiredService<IAssessmentReader>();
                var now = DateTimeOffset.UtcNow;
                var candidate = await db.Jobs.AsNoTracking().Where(j =>
                    ((j.Status == "queued" || j.Status == "retry") && j.NextRunAt <= now)
                    || (j.Status == "processing" && j.LeaseUntil <= now))
                    .OrderBy(j => j.NextRunAt).FirstOrDefaultAsync(stoppingToken);
                if (candidate is null)
                {
                    await CleanupDeletedAsync(db, reader, stoppingToken);
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                var fence = candidate.Fence + 1;
                var claimed = await db.Jobs.Where(j => j.Id == candidate.Id && j.Fence == candidate.Fence
                    && (((j.Status == "queued" || j.Status == "retry") && j.NextRunAt <= now)
                        || (j.Status == "processing" && j.LeaseUntil <= now)))
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "processing")
                        .SetProperty(j => j.Fence, fence).SetProperty(j => j.LeaseUntil, now.AddSeconds(LeaseSeconds)), stoppingToken);
                if (claimed == 0) continue;
                candidate.Fence = fence;
                await ProcessAsync(db, reader, scope.ServiceProvider.GetRequiredService<AiProviders>(), candidate, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Evaluation worker paused ({FailureType})", exception.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(EvaluationDb db, IAssessmentReader reader, AiProviders providers,
        Job job, CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await reader.GetEvidenceAsync(job.Id, cancellationToken);
            if (evidence is null)
            {
                await db.Jobs.Where(j => j.Id == job.Id && j.Fence == job.Fence).ExecuteDeleteAsync(cancellationToken);
                return;
            }
            if (evidence.ManualOnly)
            {
                await FinishAsync(db, reader, job, "needs-review", "manual-only", null, cancellationToken);
                return;
            }
            var answer = evidence.Answer;
            if (evidence.Question.Kind == "oral")
            {
                if (evidence.Recording is null)
                {
                    await FinishAsync(db, reader, job, "needs-review", "missing-recording", null, cancellationToken);
                    return;
                }
                answer = job.Transcript ?? "";
                if (job.Transcript is null)
                {
                    var recording = await reader.OpenRecordingAsync(job.AttemptId, evidence.Recording.Id, cancellationToken);
                    if (recording is null) throw new ProviderFailure("recording-unavailable");
                    await using (recording.Stream)
                        answer = await providers.TranscribeAsync(recording, cancellationToken);
                    var saved = await Current(db, job).ExecuteUpdateAsync(s => s.SetProperty(j => j.Transcript, answer)
                        .SetProperty(j => j.LeaseUntil, DateTimeOffset.UtcNow.AddSeconds(LeaseSeconds)), cancellationToken);
                    if (saved == 0) return;
                    logger.LogInformation("Transcript persisted for intent {IntentId}", job.Id);
                }
            }
            if (string.IsNullOrWhiteSpace(answer))
            {
                await FinishAsync(db, reader, job, "needs-review", "unanswered", null, cancellationToken);
                return;
            }
            var suggestion = await providers.ScoreAsync(evidence, answer, cancellationToken);
            await FinishAsync(db, reader, job, "ready", null, EvaluationJson.Write(suggestion), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            var failures = job.Failures + 1;
            var code = exception is ProviderFailure failure ? failure.Code : "provider-unavailable";
            var terminal = failures >= 4;
            await Current(db, job).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, terminal ? "needs-review" : "retry")
                .SetProperty(j => j.Error, code).SetProperty(j => j.Failures, failures)
                .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(j => j.NextRunAt, DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * Math.Pow(2, failures - 1)))), cancellationToken);
            logger.LogWarning("Evaluation intent {IntentId} {State} after provider failure ({Code})",
                job.Id, terminal ? "needs human review" : "scheduled for retry", code);
        }
    }

    private static IQueryable<Job> Current(EvaluationDb db, Job job) => db.Jobs.Where(j => j.Id == job.Id
        && j.Fence == job.Fence && j.Status == "processing" && j.LeaseUntil > DateTimeOffset.UtcNow);

    private static async Task FinishAsync(EvaluationDb db, IAssessmentReader reader, Job job, string status,
        string? error, string? suggestion, CancellationToken cancellationToken)
    {
        if (await reader.IsDeletedAsync(job.AttemptId, cancellationToken))
        {
            await db.Jobs.Where(j => j.Id == job.Id && j.Fence == job.Fence).ExecuteDeleteAsync(cancellationToken);
            return;
        }
        await Current(db, job).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status)
            .SetProperty(j => j.Error, error).SetProperty(j => j.SuggestionJson, suggestion)
            .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null), cancellationToken);
    }

    private static async Task CleanupDeletedAsync(EvaluationDb db, IAssessmentReader reader, CancellationToken cancellationToken)
    {
        var ids = await db.Jobs.Select(j => j.AttemptId).Union(db.Publications.Select(p => p.AttemptId))
            .Distinct().ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            if (!await reader.IsDeletedAsync(id, cancellationToken)) continue;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Jobs.Where(j => j.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
            await db.Reviews.Where(j => j.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
            await db.Publications.Where(j => j.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
            await db.Audits.Where(j => j.AttemptId == id).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}

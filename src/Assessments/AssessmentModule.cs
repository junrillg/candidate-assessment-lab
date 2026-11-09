using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Assessments;

public static partial class AssessmentModule
{
    public static IServiceCollection AddAssessments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AssessmentDb>(options => options.UseNpgsql(configuration.GetConnectionString("Database"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "assessments")));
        services.AddSingleton<RecordingStorage>();
        services.AddScoped<IAssessmentReader, AssessmentReader>();
        services.AddHostedService<AssessmentMaintenance>();
        return services;
    }
    public static async Task MigrateAssessmentsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AssessmentDb>().Database.MigrateAsync(cancellationToken);
    }
    public static WebApplication MapAssessments(this WebApplication app)
    {
        app.MapGet("/api/assignments", ListAssignments).RequireAuthorization("Candidate");
        app.MapPost("/api/assignments/{id:guid}/start", StartAttempt).RequireAuthorization("Candidate");
        app.MapGet("/api/attempts/{id:guid}", GetAttempt).RequireAuthorization();
        app.MapPut("/api/attempts/{id:guid}/answers/{questionId:guid}", SaveAnswer).RequireAuthorization("Candidate");
        app.MapPost("/api/attempts/{id:guid}/recordings/{questionId:guid}/{uploadId:guid}", UploadRecording).RequireAuthorization("Candidate");
        app.MapGet("/api/attempts/{id:guid}/recordings/{recordingId:guid}", ReadRecording).RequireAuthorization();
        app.MapPost("/api/attempts/{id:guid}/submit", SubmitAttempt).RequireAuthorization("Candidate");
        app.MapDelete("/api/attempts/{id:guid}", DeleteAttempt).RequireAuthorization();
        return app;
    }
    private static async Task<IResult> ListAssignments(ClaimsPrincipal user, AssessmentDb db, CancellationToken cancellationToken)
    {
        var owner = UserId(user);
        return Results.Ok(await db.Assignments.AsNoTracking().Where(x => x.CandidateId == owner).Select(x => new {
            x.Id, x.Title, x.DurationMinutes,
            AttemptId = db.Attempts.Where(a => a.AssignmentId == x.Id && a.DeletedAt == null).Select(a => (Guid?)a.Id).FirstOrDefault()
        }).ToListAsync(cancellationToken));
    }
    private static async Task<IResult> StartAttempt(Guid id, StartRequest request, ClaimsPrincipal user, AssessmentDb db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var assignment = await db.Assignments.FromSqlInterpolated($"SELECT * FROM assessments.assignments WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (assignment is null || assignment.CandidateId != UserId(user)) return Results.NotFound();
        var existingId = await db.Attempts.Where(x => x.AssignmentId == id).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var attempt = existingId is Guid previousId ? await LockAsync(db, previousId, cancellationToken) : null;
        if (attempt?.DeletedAt is not null) return Error(409, "This assignment's attempt was deleted. Ask your reviewer for a new assignment.");
        if (attempt is null)
        {
            var now = await Now(db, cancellationToken);
            attempt = new Attempt { Id = Guid.NewGuid(), AssignmentId = id, CandidateId = assignment.CandidateId,
                Title = assignment.Title, StartedAt = now, Deadline = now.AddMinutes(assignment.DurationMinutes),
                QuestionsJson = assignment.QuestionsJson, ManualOnly = request.ManualOnly };
            db.Attempts.Add(attempt);
            foreach (var question in Questions(attempt)) db.Answers.Add(new Answer { AttemptId = attempt.Id, QuestionId = question.Id });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (attempt.SubmittedAt is null && await Now(db, cancellationToken) >= attempt.Deadline) await FinalizeAsync(attempt, db, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(await ViewAsync(attempt, db, cancellationToken));
    }
    private static async Task<IResult> GetAttempt(Guid id, ClaimsPrincipal user, AssessmentDb db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await LockAsync(db, id, cancellationToken);
        if (!CanRead(attempt, user)) return Results.NotFound();
        if (attempt!.SubmittedAt is null && await Now(db, cancellationToken) >= attempt.Deadline) await FinalizeAsync(attempt, db, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(await ViewAsync(attempt, db, cancellationToken));
    }
    private static async Task<IResult> SaveAnswer(Guid id, Guid questionId, SaveRequest request, ClaimsPrincipal user, AssessmentDb db, CancellationToken cancellationToken)
    {
        if (request.Revision < 0 || request.Text is null || request.Text.Length > 20000) return Error(400, "An answer needs its current revision and at most 20,000 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await LockAsync(db, id, cancellationToken);
        if (!IsOwner(attempt, user)) return Results.NotFound();
        var now = await Now(db, cancellationToken);
        if (attempt!.SubmittedAt is not null || now >= attempt.Deadline)
        {
            if (attempt.SubmittedAt is null) await FinalizeAsync(attempt, db, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Error(409, "This attempt is submitted. Local changes were not accepted.");
        }
        var question = Questions(attempt).SingleOrDefault(x => x.Id == questionId);
        if (question is null) return Results.NotFound();
        var answer = await db.Answers.SingleAsync(x => x.AttemptId == id && x.QuestionId == questionId, cancellationToken);
        if (answer.Revision != request.Revision) return Results.Json(new { error = "Another tab saved a newer answer. Choose which version to keep.", current = AnswerView(answer) }, statusCode: 409);
        if (request.RecordingId is not null && (question.Kind != "oral" || !await db.Recordings.AnyAsync(x => x.Id == request.RecordingId && x.AttemptId == id && x.QuestionId == questionId && x.PurgedAt == null, cancellationToken)))
            return Error(400, "Select a verified recording belonging to this question.");
        answer.Text = request.Text;
        answer.Flagged = request.Flagged;
        answer.RecordingId = request.RecordingId;
        answer.Revision++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(AnswerView(answer));
    }
    private static async Task<IResult> UploadRecording(Guid id, Guid questionId, Guid uploadId, HttpContext http, AssessmentDb db, RecordingStorage storage, CancellationToken cancellationToken)
    {
        var access = await db.Attempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (!IsOwner(access, http.User)) return Results.NotFound();
        if (!Questions(access!).Any(x => x.Id == questionId && x.Kind == "oral")) return Results.NotFound();
        if (http.Request.ContentLength > RecordingStorage.MaximumBytes) return Error(413, "Recording exceeds the 10 MB limit.");
        VerifiedUpload upload;
        try { upload = await storage.VerifyAsync(http.Request.Body, http.Request.ContentType ?? "", cancellationToken); }
        catch (InvalidRecordingException exception) { return Error(400, exception.Message); }
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var attempt = await LockAsync(db, id, cancellationToken);
            if (!IsOwner(attempt, http.User)) return Results.NotFound();
            var existing = await db.Recordings.SingleOrDefaultAsync(x => x.AttemptId == id && x.UploadId == uploadId, cancellationToken);
            if (existing is not null)
                return existing.QuestionId == questionId && existing.Sha256 == upload.Sha256 && existing.ContentType == upload.ContentType && existing.PurgedAt is null
                    ? Results.Ok(RecordingView(existing)) : Error(409, "This upload ID already identifies different or expired audio. Keep the original ID only when retrying the same file.");
            if (attempt!.SubmittedAt is not null || await Now(db, cancellationToken) >= attempt.Deadline)
            {
                if (attempt.SubmittedAt is null) await FinalizeAsync(attempt, db, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Error(409, "The deadline has passed. This recording remains local and was not accepted.");
            }
            var recording = new Recording { Id = Guid.NewGuid(), AttemptId = id, QuestionId = questionId, UploadId = uploadId,
                ContentType = upload.ContentType, Size = upload.Size, Sha256 = upload.Sha256, CreatedAt = await Now(db, cancellationToken) };
            recording.Path = storage.Commit(upload, recording.Id);
            db.Recordings.Add(recording);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(RecordingView(recording));
        }
        finally
        {
            File.Delete(upload.TemporaryPath);
            // A lost commit acknowledgement can still mean success; orphan cleanup removes unreferenced files later.
        }
    }
    private static async Task<IResult> ReadRecording(Guid id, Guid recordingId, HttpContext http, AssessmentDb db, RecordingStorage storage, CancellationToken cancellationToken)
    {
        var attempt = await db.Attempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (!CanRead(attempt, http.User)) return Results.NotFound();
        var recording = await db.Recordings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordingId && x.AttemptId == id && x.PurgedAt == null, cancellationToken);
        var content = recording is null ? null : storage.Open(recording);
        if (content is null) return Results.NotFound();
        http.Response.Headers.CacheControl = "no-store, private";
        return Results.Stream(content.Stream, content.ContentType, enableRangeProcessing: true);
    }
    private static async Task<IResult> SubmitAttempt(Guid id, ClaimsPrincipal user, AssessmentDb db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await LockAsync(db, id, cancellationToken);
        if (!IsOwner(attempt, user)) return Results.NotFound();
        if (attempt!.SubmittedAt is null) await FinalizeAsync(attempt, db, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { attemptId = attempt.Id, receipt = attempt.Receipt, submittedAt = attempt.SubmittedAt });
    }
    private static async Task<IResult> DeleteAttempt(Guid id, ClaimsPrincipal user, AssessmentDb db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await LockAsync(db, id, cancellationToken);
        if (attempt is null || (attempt.CandidateId != UserId(user) && !user.IsInRole("Reviewer"))) return Results.NotFound();
        attempt.DeletedAt ??= await Now(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Accepted(value: new { status = "deletion-pending", attemptId = id });
    }
    internal static async Task FinalizeAsync(Attempt attempt, AssessmentDb db, CancellationToken cancellationToken)
    {
        if (attempt.SubmittedAt is not null || attempt.DeletedAt is not null) return;
        var now = await Now(db, cancellationToken);
        attempt.SubmittedAt = now >= attempt.Deadline ? attempt.Deadline : now;
        attempt.Receipt = $"CA-{Guid.NewGuid():N}";
        var answers = await db.Answers.Where(x => x.AttemptId == attempt.Id).ToDictionaryAsync(x => x.QuestionId, cancellationToken);
        var recordings = await db.Recordings.Where(x => x.AttemptId == attempt.Id && x.PurgedAt == null).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var question in Questions(attempt))
        {
            var answer = answers[question.Id];
            var recording = answer.RecordingId is Guid recordingId ? recordings.GetValueOrDefault(recordingId) : null;
            var intent = new ProcessingIntent(Guid.NewGuid(), attempt.Id, question.Id, attempt.Id);
            var evidence = new QuestionEvidence(intent, attempt.CandidateId, attempt.Title, attempt.ManualOnly, question, answer.Text,
                recording is null ? null : new RecordingReference(recording.Id, recording.ContentType, recording.Size, recording.Sha256, recording.CreatedAt), attempt.SubmittedAt.Value);
            db.Submissions.Add(new Submission { Id = intent.Id, AttemptId = attempt.Id, QuestionId = question.Id, EvidenceJson = JsonSerializer.Serialize(evidence) });
            db.Outbox.Add(new Outbox { Id = intent.Id, AttemptId = attempt.Id, QuestionId = question.Id, CorrelationId = intent.CorrelationId });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
    internal static Task<Attempt?> LockAsync(AssessmentDb db, Guid id, CancellationToken cancellationToken) =>
        db.Attempts.FromSqlInterpolated($"SELECT * FROM assessments.attempts WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    internal static async Task<DateTimeOffset> Now(AssessmentDb db, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(cancellationToken);
    internal static AssessmentQuestion[] Questions(Attempt attempt) => JsonSerializer.Deserialize<AssessmentQuestion[]>(attempt.QuestionsJson)!;
    private static Guid UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private static bool IsOwner(Attempt? attempt, ClaimsPrincipal user) => attempt is { DeletedAt: null } && attempt.CandidateId == UserId(user);
    private static bool CanRead(Attempt? attempt, ClaimsPrincipal user) => attempt is { DeletedAt: null } && (attempt.CandidateId == UserId(user) || user.IsInRole("Reviewer"));
    private static IResult Error(int status, string error) => Results.Json(new { error }, statusCode: status);
    private static object AnswerView(Answer answer) => new { answer.QuestionId, answer.Revision, answer.Text, answer.Flagged, answer.RecordingId };
    private static object RecordingView(Recording recording) => new { recording.Id, recording.QuestionId, recording.Size, recording.ContentType };
    private static async Task<object> ViewAsync(Attempt attempt, AssessmentDb db, CancellationToken cancellationToken) => new {
        attempt.Id, attempt.Title, status = attempt.SubmittedAt is null ? "active" : "submitted", attempt.StartedAt, attempt.Deadline,
        serverTime = await Now(db, cancellationToken), attempt.ManualOnly, attempt.Receipt, questions = Questions(attempt),
        answers = (await db.Answers.AsNoTracking().Where(x => x.AttemptId == attempt.Id).ToListAsync(cancellationToken)).Select(AnswerView)
    };
    private sealed record StartRequest(bool ManualOnly);
    private sealed record SaveRequest(int Revision, string? Text, bool Flagged, Guid? RecordingId);
}

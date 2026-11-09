using System.Security.Claims;
using System.Text.Json;
using CandidateAssessment.Assessments;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.Evaluation;

public static class EvaluationModule
{
    public static IServiceCollection AddEvaluation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EvaluationDb>(options => options.UseNpgsql(configuration.GetConnectionString("Database"),
            pg => pg.MigrationsHistoryTable("__migrations", "evaluation")));
        services.AddScoped<AiProviders>();
        ConfigureProvider(services, configuration, "ollama", "Providers:OllamaUrl", "http://host.docker.internal:11434/");
        ConfigureProvider(services, configuration, "transcription", "Providers:TranscriptionUrl", "http://transcription:8081/");
        services.AddHostedService<IntentConsumer>();
        services.AddHostedService<EvaluationWorker>();
        return services;
    }

    private static void ConfigureProvider(IServiceCollection services, IConfiguration configuration, string name, string key, string fallback)
    {
        var url = configuration[key] ?? fallback;
        if (!Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException($"Invalid configuration: {key}");
        services.AddHttpClient(name, client =>
        {
            client.BaseAddress = uri;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Providers:TimeoutSeconds", 120), 5, 150));
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10), MaxConnectionsPerServer = 4
        });
    }

    public static async Task MigrateEvaluationAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EvaluationDb>();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS evaluation", cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    public static WebApplication MapEvaluation(this WebApplication app)
    {
        app.MapGet("/api/evaluation/{attemptId:guid}", GetEvaluationAsync).RequireAuthorization();
        app.MapGet("/api/reviews", ListReviewsAsync).RequireAuthorization("Reviewer");
        app.MapGet("/api/reviews/{attemptId:guid}/audit", GetAuditAsync).RequireAuthorization("Reviewer");
        app.MapPut("/api/reviews/{attemptId:guid}/questions/{questionId:guid}", SaveReviewAsync).RequireAuthorization("Reviewer");
        app.MapPost("/api/reviews/{attemptId:guid}/questions/{questionId:guid}/retry", RetryAsync).RequireAuthorization("Reviewer");
        app.MapPost("/api/reviews/{attemptId:guid}/publish", PublishAsync).RequireAuthorization("Reviewer");
        return app;
    }

    private static async Task<IResult> GetEvaluationAsync(Guid attemptId, HttpContext http, IAssessmentReader reader,
        EvaluationDb db, CancellationToken cancellationToken)
    {
        var attempt = await reader.GetSubmittedAttemptAsync(attemptId, cancellationToken);
        if (attempt is null) return Error(404, "Submitted attempt not found.");
        var reviewer = http.User.IsInRole("Reviewer");
        if (!reviewer && attempt.CandidateId != UserId(http)) return Error(404, "Submitted attempt not found.");
        var jobs = await db.Jobs.AsNoTracking().Where(j => j.AttemptId == attemptId).ToListAsync(cancellationToken);
        var reviews = await db.Reviews.AsNoTracking().Where(j => j.AttemptId == attemptId).ToListAsync(cancellationToken);
        var publication = await db.Publications.AsNoTracking().SingleOrDefaultAsync(p => p.AttemptId == attemptId, cancellationToken);
        var status = OverallStatus(attempt, jobs, reviews, publication);
        if (!reviewer)
        {
            var published = publication?.PublishedAt is not null;
            return Results.Ok(new
            {
                attemptId, status, publishedAt = publication?.PublishedAt,
                feedback = published ? publication!.Feedback : null, total = published ? publication!.Total : null,
                maximum = published ? publication!.Maximum : null,
                questions = attempt.Questions.Select(q =>
                {
                    var job = jobs.SingleOrDefault(j => j.QuestionId == q.Id);
                    var review = reviews.SingleOrDefault(j => j.QuestionId == q.Id);
                    return new
                    {
                        questionId = q.Id, status = published ? "reviewed" : PublicJobStatus(job),
                        review = published && review is not null ? new
                        {
                            criteria = EvaluationJson.Read<CriterionScore[]>(review.CriteriaJson), review.Feedback
                        } : null
                    };
                })
            });
        }
        return Results.Ok(new
        {
            attemptId, status, revision = publication?.Revision ?? 0, publication?.PublishedAt,
            publication?.Feedback, publication?.Total, publication?.Maximum,
            questions = attempt.Questions.Select(q =>
            {
                var job = jobs.SingleOrDefault(j => j.QuestionId == q.Id);
                var review = reviews.SingleOrDefault(j => j.QuestionId == q.Id);
                return new
                {
                    questionId = q.Id, status = review is not null ? "reviewed" : job?.Status ?? "awaiting-delivery",
                    transcript = job?.Transcript,
                    suggestion = job?.SuggestionJson is null ? null : EvaluationJson.Read<Suggestion>(job.SuggestionJson),
                    error = job?.Error, failures = job?.Failures ?? 0, nextRunAt = job?.Status == "retry" ? job.NextRunAt : (DateTimeOffset?)null,
                    review = review is null ? null : new
                    {
                        criteria = EvaluationJson.Read<CriterionScore[]>(review.CriteriaJson), review.Feedback,
                        review.InternalNotes, review.Revision, review.ReviewerId, review.UpdatedAt
                    }
                };
            })
        });
    }

    private static async Task<IResult> ListReviewsAsync(IAssessmentReader reader, EvaluationDb db, CancellationToken cancellationToken)
    {
        var attempts = await reader.ListSubmittedAttemptsAsync(cancellationToken);
        var jobs = await db.Jobs.AsNoTracking().ToListAsync(cancellationToken);
        var reviews = await db.Reviews.AsNoTracking().ToListAsync(cancellationToken);
        var publications = await db.Publications.AsNoTracking().ToListAsync(cancellationToken);
        return Results.Ok(attempts.OrderByDescending(a => a.SubmittedAt).Select(a => new
        {
            attemptId = a.Id, a.Title, a.CandidateId, a.SubmittedAt,
            status = OverallStatus(a, jobs.Where(j => j.AttemptId == a.Id).ToList(),
                reviews.Where(r => r.AttemptId == a.Id).ToList(), publications.SingleOrDefault(p => p.AttemptId == a.Id))
        }));
    }

    private static async Task<IResult> SaveReviewAsync(Guid attemptId, Guid questionId, ReviewRequest request,
        HttpContext http, IAssessmentReader reader, EvaluationDb db, CancellationToken cancellationToken)
    {
        var attempt = await reader.GetSubmittedAttemptAsync(attemptId, cancellationToken);
        var question = attempt?.Questions.SingleOrDefault(q => q.Id == questionId);
        if (question is null) return Error(404, "Submitted question not found.");
        if (!ValidScores(request.Criteria, question.Rubric) || request.Revision < 0 || request.Feedback is null
            || request.InternalNotes is null || request.Feedback.Length > 4000 || request.InternalNotes.Length > 4000)
            return Error(400, "Provide each rubric criterion once, with an integer score from 0 to 5; notes are limited to 4000 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var publication = await LockPublicationAsync(db, attemptId, cancellationToken);
        if (publication.PublishedAt is not null) return Error(409, "Published reviews are immutable.");
        var review = await db.Reviews.SingleOrDefaultAsync(r => r.AttemptId == attemptId && r.QuestionId == questionId, cancellationToken);
        if ((review?.Revision ?? 0) != request.Revision) return Error(409, "This review changed in another tab. Reload before saving.");
        var previous = review?.CriteriaJson;
        if (review is null)
        {
            review = new Review { AttemptId = attemptId, QuestionId = questionId };
            db.Reviews.Add(review);
        }
        review.CriteriaJson = EvaluationJson.Write(request.Criteria);
        review.Feedback = request.Feedback.Trim();
        review.InternalNotes = request.InternalNotes.Trim();
        review.ReviewerId = UserId(http);
        review.UpdatedAt = DateTimeOffset.UtcNow;
        review.Revision++;
        publication.Revision++;
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.AttemptId == attemptId && j.QuestionId == questionId, cancellationToken);
        AddAudit(db, http, attemptId, questionId, "save-review", new
        {
            previous = previous is null ? null : EvaluationJson.Read<CriterionScore[]>(previous),
            current = request.Criteria,
            suggestion = job?.SuggestionJson is null ? null : EvaluationJson.Read<Suggestion>(job.SuggestionJson).Criteria
                .Select(x => new CriterionScore(x.CriterionId, x.Score)).ToArray()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { questionId, revision = review.Revision, attemptRevision = publication.Revision });
    }

    private static async Task<IResult> PublishAsync(Guid attemptId, PublishRequest request, HttpContext http,
        IAssessmentReader reader, EvaluationDb db, CancellationToken cancellationToken)
    {
        var attempt = await reader.GetSubmittedAttemptAsync(attemptId, cancellationToken);
        if (attempt is null) return Error(404, "Submitted attempt not found.");
        if (request.Feedback is null || request.Feedback.Length > 4000 || request.Revision < 0)
            return Error(400, "Feedback is limited to 4000 characters and a current review revision is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var publication = await LockPublicationAsync(db, attemptId, cancellationToken);
        if (publication.PublishedAt is not null)
            return Results.Ok(new { attemptId, publication.PublishedAt, publication.Total, publication.Maximum });
        if (publication.Revision != request.Revision) return Error(409, "The review changed. Reload before publishing.");
        var reviews = await db.Reviews.Where(r => r.AttemptId == attemptId).ToListAsync(cancellationToken);
        if (reviews.Count != attempt.Questions.Count || attempt.Questions.Any(q =>
            !reviews.Any(r => r.QuestionId == q.Id && ValidScores(EvaluationJson.Read<CriterionScore[]>(r.CriteriaJson), q.Rubric))))
            return Error(409, "Score every criterion of every question before publishing.");
        publication.Feedback = request.Feedback.Trim();
        publication.Total = reviews.Sum(r => EvaluationJson.Read<CriterionScore[]>(r.CriteriaJson).Sum(x => x.Score));
        publication.Maximum = attempt.Questions.Sum(q => q.Rubric.Count * 5);
        publication.PublishedAt = DateTimeOffset.UtcNow;
        publication.ReviewerId = UserId(http);
        publication.Revision++;
        AddAudit(db, http, attemptId, null, "publish-review", new { publication.Total, publication.Maximum });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { attemptId, publication.PublishedAt, publication.Total, publication.Maximum });
    }

    private static async Task<IResult> RetryAsync(Guid attemptId, Guid questionId, HttpContext http,
        IAssessmentReader reader, EvaluationDb db, CancellationToken cancellationToken)
    {
        var attempt = await reader.GetSubmittedAttemptAsync(attemptId, cancellationToken);
        if (attempt is null || !attempt.Questions.Any(q => q.Id == questionId)) return Error(404, "Submitted question not found.");
        if (attempt.ManualOnly) return Error(409, "This candidate selected manual-only review.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var publication = await LockPublicationAsync(db, attemptId, cancellationToken);
        if (publication.PublishedAt is not null) return Error(409, "Published reviews are immutable.");
        var now = DateTimeOffset.UtcNow;
        var updated = await db.Jobs.Where(j => j.AttemptId == attemptId && j.QuestionId == questionId
            && j.Status == "needs-review" && j.Error != "manual-only" && j.Error != "unanswered" && j.Error != "missing-recording")
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "queued").SetProperty(j => j.Error, (string?)null)
                .SetProperty(j => j.Failures, 0).SetProperty(j => j.Fence, j => j.Fence + 1)
                .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null).SetProperty(j => j.NextRunAt, now), cancellationToken);
        if (updated == 0) return Error(409, "Only a failed provider job can be retried.");
        publication.Revision++;
        AddAudit(db, http, attemptId, questionId, "retry-processing", new { });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { questionId, status = "queued", attemptRevision = publication.Revision });
    }

    private static async Task<IResult> GetAuditAsync(Guid attemptId, IAssessmentReader reader,
        EvaluationDb db, CancellationToken cancellationToken)
    {
        if (await reader.GetSubmittedAttemptAsync(attemptId, cancellationToken) is null) return Error(404, "Submitted attempt not found.");
        return Results.Ok(await db.Audits.AsNoTracking().Where(a => a.AttemptId == attemptId).OrderBy(a => a.At)
            .Select(a => new { a.Id, a.QuestionId, a.ReviewerId, a.Action, a.DetailJson, a.At }).ToListAsync(cancellationToken));
    }

    private static async Task<Publication> LockPublicationAsync(EvaluationDb db, Guid attemptId, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO evaluation.publications ("AttemptId","Revision","Feedback") VALUES ({attemptId},0,'') ON CONFLICT DO NOTHING
            """, cancellationToken);
        return await db.Publications.FromSqlInterpolated($"""
            SELECT * FROM evaluation.publications WHERE "AttemptId"={attemptId} FOR UPDATE
            """).SingleAsync(cancellationToken);
    }

    private static void AddAudit(EvaluationDb db, HttpContext http, Guid attemptId, Guid? questionId, string action, object detail) =>
        db.Audits.Add(new Audit { Id = Guid.NewGuid(), AttemptId = attemptId, QuestionId = questionId,
            ReviewerId = UserId(http), Action = action, DetailJson = EvaluationJson.Write(detail), At = DateTimeOffset.UtcNow });

    internal static bool ValidScores(IReadOnlyList<CriterionScore>? scores, IReadOnlyList<RubricCriterion> rubric) =>
        scores is not null && scores.Count == rubric.Count && !scores.Any(x => x is null)
        && scores.Select(s => s.CriterionId).Distinct(StringComparer.Ordinal).Count() == rubric.Count
        && scores.All(s => s.Score is >= 0 and <= 5 && rubric.Any(r => r.Id == s.CriterionId));

    private static string OverallStatus(SubmittedAttempt attempt, IReadOnlyList<Job> jobs, IReadOnlyList<Review> reviews, Publication? publication)
    {
        if (publication?.PublishedAt is not null) return "published";
        if (reviews.Count == attempt.Questions.Count) return "ready-to-publish";
        if (reviews.Count > 0) return "in-review";
        if (jobs.Any(j => j.Status == "needs-review")) return "needs-review";
        if (jobs.Count == attempt.Questions.Count && jobs.All(j => j.Status == "ready")) return "ready-for-review";
        return "processing";
    }

    private static string PublicJobStatus(Job? job) => job?.Status switch
    {
        "ready" => "awaiting-review", "needs-review" => "needs-review", _ => "processing"
    };
    private static Guid UserId(HttpContext http) => Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
    internal sealed record ReviewRequest(IReadOnlyList<CriterionScore>? Criteria, string Feedback, string InternalNotes, int Revision);
    internal sealed record PublishRequest(string Feedback, int Revision);
}

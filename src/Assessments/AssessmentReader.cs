using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.Assessments;

internal sealed class AssessmentReader(AssessmentDb db, RecordingStorage storage) : IAssessmentReader
{
    public async Task<QuestionEvidence?> GetEvidenceAsync(Guid processingIntentId, CancellationToken cancellationToken = default)
    {
        var submission = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == processingIntentId, cancellationToken);
        if (submission is null || await IsDeletedAsync(submission.AttemptId, cancellationToken)) return null;
        return JsonSerializer.Deserialize<QuestionEvidence>(submission.EvidenceJson);
    }
    public async Task<SubmittedAttempt?> GetSubmittedAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await db.Attempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId && x.DeletedAt == null && x.SubmittedAt != null, cancellationToken);
        return attempt is null ? null : ToSubmitted(attempt);
    }
    public async Task<IReadOnlyList<SubmittedAttempt>> ListSubmittedAttemptsAsync(CancellationToken cancellationToken = default)
    {
        var attempts = await db.Attempts.AsNoTracking().Where(x => x.DeletedAt == null && x.SubmittedAt != null).OrderByDescending(x => x.SubmittedAt).ToListAsync(cancellationToken);
        return attempts.Select(ToSubmitted).ToArray();
    }
    public async Task<AttemptAccess?> GetAttemptAccessAsync(Guid attemptId, CancellationToken cancellationToken = default) =>
        await db.Attempts.AsNoTracking().Where(x => x.Id == attemptId)
            .Select(x => new AttemptAccess(x.CandidateId, x.SubmittedAt != null, x.DeletedAt != null)).SingleOrDefaultAsync(cancellationToken);
    public async Task<bool> IsDeletedAsync(Guid attemptId, CancellationToken cancellationToken = default) =>
        !await db.Attempts.AnyAsync(x => x.Id == attemptId && x.DeletedAt == null, cancellationToken);
    public async Task<RecordingContent?> OpenRecordingAsync(Guid attemptId, Guid recordingId, CancellationToken cancellationToken = default)
    {
        if (await IsDeletedAsync(attemptId, cancellationToken)) return null;
        var recording = await db.Recordings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordingId && x.AttemptId == attemptId && x.PurgedAt == null, cancellationToken);
        if (recording is null) return null;
        return storage.Open(recording);
    }
    private static SubmittedAttempt ToSubmitted(Attempt attempt) => new(attempt.Id, attempt.CandidateId, attempt.Title,
        attempt.SubmittedAt!.Value, attempt.ManualOnly, JsonSerializer.Deserialize<AssessmentQuestion[]>(attempt.QuestionsJson)!);
}

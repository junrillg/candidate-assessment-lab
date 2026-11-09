namespace CandidateAssessment.Assessments;

public sealed record RubricCriterion(string Id, string Label, IReadOnlyList<string> Anchors);
public sealed record AssessmentQuestion(Guid Id, int Number, string Kind, string Title, string Prompt,
    IReadOnlyList<RubricCriterion> Rubric, string Version);
public sealed record ProcessingIntent(Guid Id, Guid AttemptId, Guid QuestionId, Guid CorrelationId);
public sealed record RecordingReference(Guid Id, string ContentType, long Size, string Sha256, DateTimeOffset CreatedAt);
public sealed record SubmittedAttempt(Guid Id, Guid CandidateId, string Title, DateTimeOffset SubmittedAt,
    bool ManualOnly, IReadOnlyList<AssessmentQuestion> Questions);
public sealed record QuestionEvidence(ProcessingIntent Intent, Guid CandidateId, string Title, bool ManualOnly,
    AssessmentQuestion Question, string Answer, RecordingReference? Recording, DateTimeOffset SubmittedAt);
public sealed record AttemptAccess(Guid CandidateId, bool Submitted, bool Deleted);
public sealed record RecordingContent(Stream Stream, string ContentType, long Length);

// This read contract returns frozen evidence, never another module's persistence entities.
public interface IAssessmentReader
{
    Task<QuestionEvidence?> GetEvidenceAsync(Guid processingIntentId, CancellationToken cancellationToken = default);
    Task<SubmittedAttempt?> GetSubmittedAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmittedAttempt>> ListSubmittedAttemptsAsync(CancellationToken cancellationToken = default);
    Task<AttemptAccess?> GetAttemptAccessAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<bool> IsDeletedAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<RecordingContent?> OpenRecordingAsync(Guid attemptId, Guid recordingId, CancellationToken cancellationToken = default);
}

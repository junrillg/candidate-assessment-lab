using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CandidateAssessment.Assessments;

internal sealed class AssessmentDb(DbContextOptions<AssessmentDb> options) : DbContext(options)
{
    internal DbSet<Assignment> Assignments => Set<Assignment>();
    internal DbSet<Attempt> Attempts => Set<Attempt>();
    internal DbSet<Answer> Answers => Set<Answer>();
    internal DbSet<Recording> Recordings => Set<Recording>();
    internal DbSet<Submission> Submissions => Set<Submission>();
    internal DbSet<Outbox> Outbox => Set<Outbox>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("assessments");
        model.Entity<Assignment>().ToTable("assignments").HasKey(x => x.Id);
        model.Entity<Assignment>().Property(x => x.QuestionsJson).HasColumnType("jsonb");
        model.Entity<Attempt>().ToTable("attempts").HasKey(x => x.Id);
        model.Entity<Attempt>().HasIndex(x => x.AssignmentId).IsUnique();
        model.Entity<Attempt>().Property(x => x.QuestionsJson).HasColumnType("jsonb");
        model.Entity<Answer>().ToTable("answers").HasKey(x => new { x.AttemptId, x.QuestionId });
        model.Entity<Answer>().Property(x => x.Revision).IsConcurrencyToken();
        model.Entity<Recording>().ToTable("recordings").HasKey(x => x.Id);
        model.Entity<Recording>().HasIndex(x => new { x.AttemptId, x.UploadId }).IsUnique();
        model.Entity<Submission>().ToTable("submissions").HasKey(x => x.Id);
        model.Entity<Submission>().HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
        model.Entity<Submission>().Property(x => x.EvidenceJson).HasColumnType("jsonb");
        model.Entity<Outbox>().ToTable("outbox").HasKey(x => x.Id);
        model.Entity<Attempt>().HasOne<Assignment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Answer>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Recording>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Submission>().HasOne<Attempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Outbox>().HasOne<Submission>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class Assignment
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string QuestionsJson { get; set; } = "[]";
}
internal sealed class Attempt
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public bool ManualOnly { get; set; }
    public string QuestionsJson { get; set; } = "[]";
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? Receipt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? CleanedAt { get; set; }
}
internal sealed class Answer
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public string Text { get; set; } = "";
    public bool Flagged { get; set; }
    public Guid? RecordingId { get; set; }
}
internal sealed class Recording
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid UploadId { get; set; }
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PurgedAt { get; set; }
}
internal sealed class Submission
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public string EvidenceJson { get; set; } = "{}";
}
internal sealed class Outbox
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

[DbContext(typeof(AssessmentDb))]
[Migration("202611090001_AssessmentsInitial")]
internal sealed class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE SCHEMA IF NOT EXISTS assessments;
        CREATE TABLE assessments.assignments (
            "Id" uuid PRIMARY KEY, "CandidateId" uuid NOT NULL, "Title" text NOT NULL,
            "DurationMinutes" integer NOT NULL CHECK ("DurationMinutes" > 0), "QuestionsJson" jsonb NOT NULL);
        CREATE TABLE assessments.attempts (
            "Id" uuid PRIMARY KEY, "AssignmentId" uuid NOT NULL UNIQUE REFERENCES assessments.assignments("Id"),
            "CandidateId" uuid NOT NULL, "Title" text NOT NULL, "StartedAt" timestamptz NOT NULL,
            "Deadline" timestamptz NOT NULL, "ManualOnly" boolean NOT NULL, "QuestionsJson" jsonb NOT NULL,
            "SubmittedAt" timestamptz, "Receipt" text, "DeletedAt" timestamptz, "CleanedAt" timestamptz);
        CREATE INDEX ON assessments.attempts ("Deadline") WHERE "SubmittedAt" IS NULL AND "DeletedAt" IS NULL;
        CREATE TABLE assessments.answers (
            "AttemptId" uuid NOT NULL REFERENCES assessments.attempts("Id"), "QuestionId" uuid NOT NULL,
            "Revision" integer NOT NULL CHECK ("Revision" >= 0), "Text" text NOT NULL CHECK (length("Text") <= 20000),
            "Flagged" boolean NOT NULL, "RecordingId" uuid, PRIMARY KEY ("AttemptId", "QuestionId"));
        CREATE TABLE assessments.recordings (
            "Id" uuid PRIMARY KEY, "AttemptId" uuid NOT NULL REFERENCES assessments.attempts("Id"),
            "QuestionId" uuid NOT NULL, "UploadId" uuid NOT NULL, "ContentType" text NOT NULL,
            "Size" bigint NOT NULL, "Sha256" text NOT NULL, "Path" text NOT NULL, "CreatedAt" timestamptz NOT NULL,
            "PurgedAt" timestamptz, UNIQUE ("AttemptId", "UploadId"));
        CREATE TABLE assessments.submissions (
            "Id" uuid PRIMARY KEY, "AttemptId" uuid NOT NULL REFERENCES assessments.attempts("Id"),
            "QuestionId" uuid NOT NULL, "EvidenceJson" jsonb NOT NULL, UNIQUE ("AttemptId", "QuestionId"));
        CREATE TABLE assessments.outbox (
            "Id" uuid PRIMARY KEY REFERENCES assessments.submissions("Id") ON DELETE CASCADE,
            "AttemptId" uuid NOT NULL, "QuestionId" uuid NOT NULL, "CorrelationId" uuid NOT NULL, "PublishedAt" timestamptz);
        CREATE INDEX ON assessments.outbox ("Id") WHERE "PublishedAt" IS NULL;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("DROP SCHEMA assessments CASCADE;");
}

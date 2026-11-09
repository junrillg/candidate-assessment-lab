using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CandidateAssessment.Evaluation;

internal sealed class EvaluationDb(DbContextOptions<EvaluationDb> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<Audit> Audits => Set<Audit>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("evaluation");
        model.Entity<Job>().ToTable("jobs").HasKey(x => x.Id);
        model.Entity<Job>().HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
        model.Entity<Job>().HasIndex(x => new { x.Status, x.NextRunAt });
        model.Entity<Review>().ToTable("reviews").HasKey(x => new { x.AttemptId, x.QuestionId });
        model.Entity<Publication>().ToTable("publications").HasKey(x => x.AttemptId);
        model.Entity<Audit>().ToTable("audit").HasKey(x => x.Id);
    }
}

internal sealed class Job
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid CorrelationId { get; set; }
    public string Status { get; set; } = "queued";
    public int Failures { get; set; }
    public int Fence { get; set; }
    public DateTimeOffset NextRunAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public string? Transcript { get; set; }
    public string? SuggestionJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class Review
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public int Revision { get; set; }
    public string CriteriaJson { get; set; } = "[]";
    public string Feedback { get; set; } = "";
    public string InternalNotes { get; set; } = "";
    public Guid ReviewerId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class Publication
{
    public Guid AttemptId { get; set; }
    public int Revision { get; set; }
    public string Feedback { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? ReviewerId { get; set; }
    public int? Total { get; set; }
    public int? Maximum { get; set; }
}

internal sealed class Audit
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid? QuestionId { get; set; }
    public Guid ReviewerId { get; set; }
    public string Action { get; set; } = "";
    public string DetailJson { get; set; } = "{}";
    public DateTimeOffset At { get; set; }
}

[DbContext(typeof(EvaluationDb))]
[Migration("202611090001_InitialEvaluation")]
internal sealed class InitialEvaluation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE evaluation.jobs (
            "Id" uuid PRIMARY KEY, "AttemptId" uuid NOT NULL, "QuestionId" uuid NOT NULL,
            "CorrelationId" uuid NOT NULL, "Status" text NOT NULL,
            "Failures" integer NOT NULL, "Fence" integer NOT NULL,
            "NextRunAt" timestamptz NOT NULL, "LeaseUntil" timestamptz,
            "Transcript" text, "SuggestionJson" text, "Error" text, "CreatedAt" timestamptz NOT NULL,
            UNIQUE ("AttemptId", "QuestionId")
        );
        CREATE INDEX jobs_due ON evaluation.jobs ("Status", "NextRunAt");
        CREATE TABLE evaluation.reviews (
            "AttemptId" uuid NOT NULL, "QuestionId" uuid NOT NULL, "Revision" integer NOT NULL,
            "CriteriaJson" text NOT NULL, "Feedback" text NOT NULL, "InternalNotes" text NOT NULL,
            "ReviewerId" uuid NOT NULL, "UpdatedAt" timestamptz NOT NULL,
            PRIMARY KEY ("AttemptId", "QuestionId")
        );
        CREATE TABLE evaluation.publications (
            "AttemptId" uuid PRIMARY KEY, "Revision" integer NOT NULL, "Feedback" text NOT NULL,
            "PublishedAt" timestamptz, "ReviewerId" uuid, "Total" integer, "Maximum" integer
        );
        CREATE TABLE evaluation.audit (
            "Id" uuid PRIMARY KEY, "AttemptId" uuid NOT NULL, "QuestionId" uuid,
            "ReviewerId" uuid NOT NULL, "Action" text NOT NULL, "DetailJson" text NOT NULL,
            "At" timestamptz NOT NULL
        );
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE evaluation.audit, evaluation.reviews, evaluation.publications, evaluation.jobs;
        """);
}

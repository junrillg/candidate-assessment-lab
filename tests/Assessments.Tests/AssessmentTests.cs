using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CandidateAssessment.Assessments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Update;
using Xunit;

namespace Assessments.Tests;

public sealed class AssessmentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "assessment-tests-" + Guid.NewGuid().ToString("N"));
    private RecordingStorage Storage => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = root }).Build());

    [Fact]
    public void ModulePublicReadContractCanBeComposedWithoutExposingPersistence()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Storage:Root"] = root, ["ConnectionStrings:Database"] = "Host=localhost;Database=not-opened;Username=test;Password=test"
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddLogging().AddAssessments(config);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsAssignableFrom<IAssessmentReader>(scope.ServiceProvider.GetRequiredService<IAssessmentReader>());
        Assert.False(typeof(IAssessmentReader).Assembly.GetType("CandidateAssessment.Assessments.AssessmentDb")!.IsPublic);
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDb>();
        var migrations = db.GetService<IMigrationsAssembly>();
        var migration = Assert.Single(migrations.Migrations);
        Assert.NotNull(migrations.CreateMigration(migration.Value, db.Database.ProviderName!));
    }
    [Fact]
    public void StartAndSubmissionWritePrincipalsBeforeTheirDependents()
    {
        using var db = new AssessmentDb(new DbContextOptionsBuilder<AssessmentDb>()
            .UseNpgsql("Host=localhost;Database=not-opened;Username=test;Password=test").Options);
        var assignmentId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        db.AddRange(
            new Assignment { Id = assignmentId },
            new Attempt { Id = attemptId, AssignmentId = assignmentId },
            new Answer { AttemptId = attemptId, QuestionId = questionId },
            new Submission { Id = intentId, AttemptId = attemptId, QuestionId = questionId },
            new Outbox { Id = intentId, AttemptId = attemptId, QuestionId = questionId });
        var adapter = db.GetService<IUpdateAdapterFactory>().Create();
        var commands = db.GetService<ICommandBatchPreparer>().BatchCommands(adapter.GetEntriesToSave(), adapter)
            .SelectMany(batch => batch.ModificationCommands).Select(command => command.TableName).ToList();
        Assert.True(commands.IndexOf("assignments") < commands.IndexOf("attempts"));
        Assert.True(commands.IndexOf("attempts") < commands.IndexOf("answers"));
        Assert.True(commands.IndexOf("attempts") < commands.IndexOf("submissions"));
        Assert.True(commands.IndexOf("submissions") < commands.IndexOf("outbox"));
    }
    [Fact]
    public void SeedFreezesTwelveQuestionsWithAnchoredCriteria()
    {
        var questions = AssessmentModule.SeedQuestions();
        Assert.Equal(8, questions.Count(x => x.Kind == "written"));
        Assert.Equal(4, questions.Count(x => x.Kind == "oral"));
        Assert.Equal(12, questions.Select(x => x.Id).Distinct().Count());
        foreach (var question in questions)
        {
            Assert.Equal("workplace-v1", question.Version);
            foreach (var criterion in question.Rubric) Assert.Equal(6, criterion.Anchors.Count);
        }
        var evidence = new QuestionEvidence(new ProcessingIntent(Guid.NewGuid(), Guid.NewGuid(), questions[0].Id, Guid.NewGuid()),
            Guid.NewGuid(), "Synthetic", false, questions[0], "The accepted answer", null, DateTimeOffset.UtcNow);
        var frozen = JsonSerializer.Serialize(evidence);
        var restored = JsonSerializer.Deserialize<QuestionEvidence>(frozen)!;
        Assert.Equal("The accepted answer", restored.Answer);
        Assert.Equal(questions[0].Prompt, restored.Question.Prompt);
        var message = JsonSerializer.Serialize(evidence.Intent, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("accepted answer", message);
        Assert.DoesNotContain("prompt", message);
        Assert.DoesNotContain("candidateId", message);
    }
    [Fact]
    public async Task RealAudioIsVerifiedBeforeItBecomesImmutableStorage()
    {
        var storage = Storage;
        var bytes = await AudioAsync(0.4);
        var upload = await storage.VerifyAsync(new MemoryStream(bytes), "audio/webm;codecs=opus", CancellationToken.None);
        Assert.Equal(bytes.Length, upload.Size);
        var id = Guid.NewGuid();
        var path = storage.Commit(upload, id);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(upload.TemporaryPath));
        var repeat = await storage.VerifyAsync(new MemoryStream(bytes), "audio/webm", CancellationToken.None);
        Assert.Equal(upload.Sha256, repeat.Sha256);
        await Assert.ThrowsAsync<IOException>(() => Task.Run(() => storage.Commit(repeat, id)));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }
    [Fact]
    public async Task RejectsMislabeledBytesAndDeletesPartialUpload()
    {
        var storage = Storage;
        await Assert.ThrowsAsync<InvalidRecordingException>(() => storage.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes("not audio")), "audio/webm", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidRecordingException>(() => storage.VerifyAsync(new MemoryStream(new byte[] { 0, 1, 2 }), "text/plain", CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "pending")));
    }
    [Fact]
    public async Task RejectsOverlongAudioAndMismatchedContainer()
    {
        var storage = Storage;
        var shortAudio = await AudioAsync(0.4);
        await Assert.ThrowsAsync<InvalidRecordingException>(() => storage.VerifyAsync(new MemoryStream(shortAudio), "audio/mp4", CancellationToken.None));
        var longAudio = await AudioAsync(120.8);
        await Assert.ThrowsAsync<InvalidRecordingException>(() => storage.VerifyAsync(new MemoryStream(longAudio), "audio/webm", CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "pending")));
    }
    [Fact]
    public async Task RejectsOversizeWithoutKeepingAnyBytes()
    {
        var storage = Storage;
        await Assert.ThrowsAsync<InvalidRecordingException>(() => storage.VerifyAsync(new MemoryStream(new byte[RecordingStorage.MaximumBytes + 1]), "audio/webm", CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, "pending")));
    }
    private async Task<byte[]> AudioAsync(double seconds)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".webm");
        var info = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=16000", "-t", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), "-c:a", "libopus", path }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await File.ReadAllBytesAsync(path);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

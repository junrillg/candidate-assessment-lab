using CandidateAssessment.Assessments;
using CandidateAssessment.Evaluation;
using Xunit;

namespace Evaluation.Tests;

public class ProviderAndReviewTests
{
    private static readonly RubricCriterion[] Rubric =
    [new("clarity", "Clarity", ["none", "weak", "partial", "clear", "strong", "exceptional"]),
     new("action", "Action", ["none", "weak", "partial", "clear", "strong", "exceptional"])];
    private const string Answer = "I would confirm the deadline and tell the team about the delay.";

    [Fact]
    public void ScoresAreOnlyAcceptedForEveryActualCriterionOnce()
    {
        Assert.True(EvaluationModule.ValidScores([new("action", 3), new("clarity", 4)], Rubric));
        Assert.False(EvaluationModule.ValidScores([new("action", 3)], Rubric));
        Assert.False(EvaluationModule.ValidScores([new("action", 3), new("action", 4)], Rubric));
        Assert.False(EvaluationModule.ValidScores([new("action", 3), new("invented", 4)], Rubric));
        Assert.False(EvaluationModule.ValidScores([new("action", -1), new("clarity", 6)], Rubric));
        Assert.False(EvaluationModule.ValidScores(null, Rubric));
    }

    [Fact]
    public void ProviderMustGroundEachValidScoreInExactAnswerEvidence()
    {
        SuggestedCriterion[] valid = [new("clarity", 4, "confirm the deadline", "Names the immediate information needed."),
            new("action", 3, "tell the team", "Identifies who should receive an update.")];
        AiProviders.ValidateSuggestion(valid, Rubric, Answer);
        var hallucination = valid.Select(x => x.CriterionId == "action" ? x with { Evidence = "schedule a meeting" } : x).ToArray();
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion(hallucination, Rubric, Answer));
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion(valid.Select(x => x with { Evidence = "" }).ToArray(), Rubric, Answer));
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion(valid.Select(x => x with { Score = 6 }).ToArray(), Rubric, Answer));
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion([valid[0], valid[0]], Rubric, Answer));
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion(null, Rubric, Answer));
    }

    [Fact]
    public void ProviderRefusesBlankRationaleAndOversizedEvidence()
    {
        SuggestedCriterion[] values = [new("clarity", 1, "confirm", ""), new("action", 1, "tell", "The answer names an action.")];
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion(values, Rubric, Answer));
        values[0] = values[0] with { Rationale = "Explains the evidence.", Evidence = new string('a', 2001) };
        Assert.Throws<ProviderFailure>(() => AiProviders.ValidateSuggestion(values, Rubric, new string('a', 2100)));
    }
}

public class ProviderHttpTests
{
    [Fact]
    public async Task TranscriptionRefusesBlankProviderOutput()
    {
        var factory = new StubClients(_ => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"text\":\"   \"}")
        });
        var provider = new AiProviders(factory, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        await using var bytes = new MemoryStream([1, 2, 3]);
        var failure = await Assert.ThrowsAsync<ProviderFailure>(() => provider.TranscribeAsync(new(bytes, "audio/webm", 3), default));
        Assert.Equal("invalid-transcript", failure.Code);
    }

    [Fact]
    public async Task ProviderResponseIsBoundedBeforeJsonParsing()
    {
        var factory = new StubClients(_ => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 262145))
        });
        var provider = new AiProviders(factory, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        await using var bytes = new MemoryStream([1, 2, 3]);
        var failure = await Assert.ThrowsAsync<ProviderFailure>(() => provider.TranscribeAsync(new(bytes, "audio/webm", 3), default));
        Assert.Equal("provider-output-too-large", failure.Code);
    }

    private sealed class StubClients(Func<HttpRequestMessage, HttpResponseMessage> respond) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(respond)) { BaseAddress = new("http://provider/") };
    }
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}

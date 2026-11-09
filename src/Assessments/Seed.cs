using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Assessments;

public static partial class AssessmentModule
{
    public static async Task SeedAssessmentsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssessmentDb>();
        var questions = JsonSerializer.Serialize(SeedQuestions());
        var seeds = new[] {
            new Assignment { Id = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"), CandidateId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Title = "Workplace judgment & communication", DurationMinutes = 20, QuestionsJson = questions },
            new Assignment { Id = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222"), CandidateId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Title = "Workplace judgment & communication", DurationMinutes = 30, QuestionsJson = questions }
        };
        foreach (var seed in seeds)
            if (!await db.Assignments.AnyAsync(x => x.Id == seed.Id, cancellationToken)) db.Assignments.Add(seed);
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static IReadOnlyList<AssessmentQuestion> SeedQuestions()
    {
        var prompts = new (string Title, string Prompt)[] {
            ("A deadline at risk", "At fictional Northstar Supply, a delivery is due tomorrow. A key check has failed and your colleague suggests skipping it. Write the message you would send to your team lead, including your next step."),
            ("Conflicting priorities", "Two colleagues at fictional Northstar Supply each need your help before noon. You can finish only one task. Explain how you would decide, what you would ask, and what you would communicate."),
            ("An unclear request", "A fictional customer asks you to 'fix the report' without explaining the problem. Write a response that clarifies the request and sets a useful next step."),
            ("A mistake in a report", "You discover that a figure you sent to a fictional customer yesterday is wrong. Explain what you would do, in what order, and how you would communicate the correction."),
            ("Sensitive information", "A fictional colleague asks you to email a customer list to their personal account to work from home. Explain your response and a safer way to help."),
            ("A process improvement", "Your fictional team spends an hour each day copying the same information between two tools. Describe a small improvement, how you would test it, and what you would measure."),
            ("Constructive disagreement", "Your fictional team plans to release a change while you still have evidence of a significant defect. Write a short message that raises the concern respectfully and proposes a decision."),
            ("A clear handover", "You are leaving a fictional support shift with one customer problem unresolved. Write a handover note that lets the next person take over safely."),
            ("Explain a setback", "In this fictional scenario, a customer is upset because their request was delayed. Record how you would acknowledge the impact, explain what you know, and agree a next step. Do not use real customer information."),
            ("Give useful feedback", "In this fictional scenario, a teammate repeatedly sends incomplete handovers. Record how you would discuss one concrete example and agree an improvement."),
            ("Explain your decision", "In this fictional scenario, you chose to delay a delivery rather than skip a safety check. Record how you would explain that decision to a manager and offer options."),
            ("Summarize a plan", "In this fictional scenario, a team must correct an inaccurate report, notify the customer, and prevent recurrence. Record a clear one-minute plan with ownership and next steps.")
        };
        var anchors = new[] {
            "0: No relevant response or evidence.", "1: Recognizes an issue but gives no workable next step.",
            "2: Gives a partial next step with a significant gap.", "3: Gives a sensible, relevant next step with basic reasoning.",
            "4: Gives clear reasoning, ownership, and a practical next step.", "5: Gives specific, proportionate reasoning, checks assumptions, and closes the loop."
        };
        return prompts.Select((p, index) => new AssessmentQuestion(
            Guid.Parse($"bbbbbbbb-0000-0000-0000-{index + 1:000000000000}"), index + 1,
            index < 8 ? "written" : "oral", p.Title, p.Prompt,
            new[] { new RubricCriterion("judgment", "Practical judgment", anchors), new RubricCriterion("clarity", "Clear communication", anchors) },
            "workplace-v1")).ToArray();
    }
}

using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReReviewPullRequestTemplate : IPromptTemplateDefinition
{
    private static readonly PromptTemplateInputDefinition PullRequestInput = new(
        "pullRequest",
        "PR",
        "#123 ou URL da PR",
        "Informe o numero ou link da PR revisada apos as correcoes.");

    private static readonly PromptTemplateInputDefinition ReviewNotesInput = new(
        "reviewNotes",
        "Pontos da revisao anterior",
        "Cole os pontos apontados na revisao anterior",
        "Informe os pontos da revisao anterior que foram enviados ao Codex para correcao.",
        Multiline: true);

    public PromptTemplateKey Key => PromptTemplateKey.ReReviewPullRequest;
    public string DisplayName => "Re-review de PR";
    public string Description => "Gera um prompt para revisar novamente uma PR apos correcoes dos pontos anteriores.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.CodeReview;
    public bool IsReReview => true;
    public PromptTemplateInputDefinition? Input => PullRequestInput;
    public IReadOnlyList<PromptTemplateInputDefinition> Inputs => new[] { PullRequestInput, ReviewNotesInput };

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken)
    {
        var pullRequestReference = PullRequestTemplateHelpers.FormatPullRequestReference(context.PullRequestReference);
        var reviewNotes = context.GetInputValue("reviewNotes")?.Trim() ?? string.Empty;

        return Task.FromResult(new RenderedPromptTemplate(
            $"Re-review {pullRequestReference}: {context.DisplayName}",
            $"""
            /review

            Re-review the {pullRequestReference} after fixes were made for the previous review findings.

            The PR implements the plan `{context.AbsolutePath}`. Use the plan as the source of truth and verify that the previous review points were actually addressed without introducing regressions.

            Previous review points passed to Codex:

            ```md
            {reviewNotes}
            ```

            Prioritize unresolved bugs, behavioral risks, regressions, and missing tests. Report findings with severity and concrete file/line references when possible. If the PR is now acceptable, say that clearly.
            """));
    }
}

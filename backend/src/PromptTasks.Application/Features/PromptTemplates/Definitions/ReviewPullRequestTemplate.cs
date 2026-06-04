using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReviewPullRequestTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReviewPullRequest;
    public string DisplayName => "Revisar PR";
    public string Description => "Gera um prompt de revisao para a PR que implementou o plano.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.CodeReview;
    public PromptTemplateInputDefinition? Input => new(
        "pullRequest",
        "PR",
        "#123 ou URL da PR",
        "Informe o numero ou link da PR criada apos a implementacao do plano.");

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken)
    {
        var pullRequestReference = PullRequestTemplateHelpers.FormatPullRequestReference(context.PullRequestReference);

        return Task.FromResult(new RenderedPromptTemplate(
            $"Review {pullRequestReference}: {context.DisplayName}",
            $"""
            /review

            Review the {pullRequestReference} that implements the plan `{context.AbsolutePath}`.

            Use the plan as the source of truth. Verify that the PR implements the plan completely, preserves the existing architecture, does not introduce regressions, and that the required validations were performed.

            Prioritize bugs, behavioral risks, and missing tests. Report findings with severity and concrete file/line references when possible.
            """));
    }
}

using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class MergePullRequestTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.MergePullRequest;
    public string DisplayName => "Fazer merge da PR";
    public string Description => "Gera um prompt para o Codex fazer merge seguro da PR.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.Merge;
    public PromptTemplateInputDefinition? Input => new(
        "pullRequest",
        "PR",
        "#123 ou URL da PR",
        "Informe o numero ou link da PR que deve ser mesclada.");

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken)
    {
        var pullRequestReference = PullRequestTemplateHelpers.FormatPullRequestReference(context.PullRequestReference);

        return Task.FromResult(new RenderedPromptTemplate(
            $"Merge {pullRequestReference}: {context.DisplayName}",
            $"""
            Merge the {pullRequestReference} that implements the plan `{context.AbsolutePath}`.

            Before merging, confirm the PR is ready to merge, the required validations passed, and preserve unrelated local changes.

            If there are conflicts or failing checks, stop and report the exact blocker. After merging, sync the local main branch with the remote, remove the worktree if it exists, delete the local/remote branch if they still exist and it is safe, and confirm the final repository state.
            """));
    }
}

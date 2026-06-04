using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class RebaseCurrentBranchTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.RebaseCurrentBranch;
    public string DisplayName => "Atualizar branch com main";
    public string Description => "Gera um prompt para atualizar a branch ou worktree atual com as ultimas alteracoes da main remota usando rebase.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.Rebase;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Update branch from main: {context.DisplayName}",
            """
            Update my current branch/worktree with the latest changes from the remote main branch using rebase.

            Preserve unrelated local changes. If there are conflicts, stop and tell me so we can resolve them together.
            """));
}

using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ImplementPlanInWorktreeTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ImplementPlanInWorktree;
    public string DisplayName => "Implementar em worktree";
    public string Description => "Gera um prompt para implementar o plano em uma worktree separada e abrir PR.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.Implementation;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Implement in worktree: {context.DisplayName}",
            $"""
            Implement the plan `{context.AbsolutePath}` completely in a separate worktree.

            Preserve the main checkout and unrelated local changes. When done, run the applicable validations, leave the branch ready for review, and open a PR.
            """));
}

using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ImplementPlanInWorktreeTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ImplementPlanInWorktree;
    public string DisplayName => "Implementar em worktree";
    public string Description => "Gera um prompt para implementar o plano em uma worktree separada e abrir PR.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Implementar em worktree: {context.DisplayName}",
            $"""
            Implemente o plano `{context.AbsolutePath}` por completo em uma worktree separada.

            Preserve o checkout principal e alteracoes locais nao relacionadas. Ao terminar, rode as validacoes cabiveis, deixe a branch pronta para revisao e crie uma PR.
            """));
}

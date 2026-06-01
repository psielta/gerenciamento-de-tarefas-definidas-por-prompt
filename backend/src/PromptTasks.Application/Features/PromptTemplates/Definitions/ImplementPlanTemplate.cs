using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ImplementPlanTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ImplementPlan;
    public string DisplayName => "Implementar plano";
    public string Description => "Gera um prompt para implementar o plano aprovado.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Implementar plano: {context.DisplayName}",
            $"Implemente o plano \"{context.AbsolutePath}\"."));
}

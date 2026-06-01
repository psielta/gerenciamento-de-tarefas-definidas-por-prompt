using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReviewPlanTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReviewPlan;
    public string DisplayName => "Revisar plano";
    public string Description => "Gera um prompt para validar, aprovar ou apontar melhorias em um plano.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.Planning;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Revisar plano: {context.DisplayName}",
            $"Dado o plano \"{context.AbsolutePath}\", valide o plano, aprove-o ou aponte melhorias."));
}

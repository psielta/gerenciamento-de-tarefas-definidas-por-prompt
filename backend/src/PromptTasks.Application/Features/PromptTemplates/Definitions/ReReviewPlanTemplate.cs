using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReReviewPlanTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReReviewPlan;
    public string DisplayName => "Re-review do plano";
    public string Description => "Gera um prompt para revalidar um plano apos Claude corrigir pontos apontados anteriormente.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.Planning;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Re-review do plano: {context.DisplayName}",
            $"Eu passei os pontos anteriores para o Claude corrigir no plano \"{context.AbsolutePath}\". Valide novamente o plano atualizado, aprove-o se estiver correto ou aponte as melhorias que ainda faltam."));
}

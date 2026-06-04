using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReReviewPlanTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReReviewPlan;
    public string DisplayName => "Re-review do plano";
    public string Description => "Gera um prompt para revalidar um plano apos Claude corrigir pontos apontados anteriormente.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.Planning;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.PlanReview;
    public bool IsReReview => true;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Re-review plan: {context.DisplayName}",
            $"I passed the previous points to Claude to fix in the plan \"{context.AbsolutePath}\". Validate the updated plan again, approve it if correct, or point out the improvements that are still missing."));
}

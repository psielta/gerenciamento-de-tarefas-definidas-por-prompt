using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReviewPlanTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReviewPlan;
    public string DisplayName => "Revisar plano";
    public string Description => "Gera um prompt para validar, aprovar ou apontar melhorias em um plano.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.Planning;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.PlanReview;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Review plan: {context.DisplayName}",
            $"Given the plan \"{context.AbsolutePath}\", validate the plan, approve it, or point out improvements."));
}

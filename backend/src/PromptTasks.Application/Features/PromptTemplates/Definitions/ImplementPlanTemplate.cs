using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ImplementPlanTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ImplementPlan;
    public string DisplayName => "Implementar plano";
    public string Description => "Gera um prompt para implementar o plano aprovado.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.General;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.Implementation;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Implement plan: {context.DisplayName}",
            $"Implement the plan \"{context.AbsolutePath}\"."));
}

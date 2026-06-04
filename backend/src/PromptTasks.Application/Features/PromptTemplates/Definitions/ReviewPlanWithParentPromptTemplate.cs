using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReviewPlanWithParentPromptTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReviewPlanWithParentPrompt;
    public string DisplayName => "Revisar plano com prompt pai";
    public string Description => "Gera um prompt de revisao incluindo o prompt original que originou o plano.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.Planning;
    public WorkflowPhaseRole? TargetPhaseRole => WorkflowPhaseRole.PlanReview;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Review plan with parent prompt: {context.DisplayName}",
            $"""
            I asked Claude to run plan-mode using the prompt below:

            ```md
            {context.ParentPromptContent}
            ```

            It generated the plan "{context.AbsolutePath}".

            Given the plan "{context.AbsolutePath}", validate the plan, approve it, or point out improvements.
            """));
}

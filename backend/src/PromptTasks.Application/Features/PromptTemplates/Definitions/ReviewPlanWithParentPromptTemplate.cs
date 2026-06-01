using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Definitions;

public sealed class ReviewPlanWithParentPromptTemplate : IPromptTemplateDefinition
{
    public PromptTemplateKey Key => PromptTemplateKey.ReviewPlanWithParentPrompt;
    public string DisplayName => "Revisar plano com prompt pai";
    public string Description => "Gera um prompt de revisao incluindo o prompt original que originou o plano.";
    public TargetAgent DefaultTargetAgent => TargetAgent.Codex;
    public PromptKind DefaultKind => PromptKind.Planning;
    public PromptTemplateInputDefinition? Input => null;

    public Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RenderedPromptTemplate(
            $"Revisar plano com prompt pai: {context.DisplayName}",
            $"""
            Eu pedi para Claude fazer um plan-mode usando o prompt abaixo:

            ```md
            {context.ParentPromptContent}
            ```

            Ele gerou o plano "{context.AbsolutePath}".

            Dado o plano "{context.AbsolutePath}", valide o plano, aprove-o ou aponte melhorias.
            """));
}

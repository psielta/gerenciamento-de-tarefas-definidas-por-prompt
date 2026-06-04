using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.PromptTemplates;

public interface IPromptTemplateDefinition
{
    PromptTemplateKey Key { get; }
    string DisplayName { get; }
    string Description { get; }
    TargetAgent DefaultTargetAgent { get; }
    PromptKind DefaultKind { get; }
    WorkflowPhaseRole? TargetPhaseRole => null;
    bool IsReReview => false;
    PromptTemplateInputDefinition? Input { get; }
    IReadOnlyList<PromptTemplateInputDefinition> Inputs =>
        Input is null
            ? Array.Empty<PromptTemplateInputDefinition>()
            : new[] { Input };

    Task<RenderedPromptTemplate> RenderAsync(
        PromptTemplateContext context,
        CancellationToken cancellationToken);
}

public sealed record RenderedPromptTemplate(string Title, string Content);

public sealed record PromptTemplateInputDefinition(
    string Key,
    string Label,
    string Placeholder,
    string HelpText,
    bool Required = true,
    bool Multiline = false);

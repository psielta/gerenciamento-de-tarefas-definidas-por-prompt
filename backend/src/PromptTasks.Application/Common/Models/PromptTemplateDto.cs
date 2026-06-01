using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Common.Models;

public sealed record PromptTemplateDto(
    PromptTemplateKey Key,
    string DisplayName,
    string Description,
    TargetAgent DefaultTargetAgent,
    PromptKind DefaultKind,
    PromptTemplateInputDto? Input);

public sealed record PromptTemplateInputDto(
    string Key,
    string Label,
    string Placeholder,
    string HelpText,
    bool Required);

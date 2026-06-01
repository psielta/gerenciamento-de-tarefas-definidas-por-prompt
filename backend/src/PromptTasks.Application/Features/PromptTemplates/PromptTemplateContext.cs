namespace PromptTasks.Application.Features.PromptTemplates;

public sealed record PromptTemplateContext(
    string AbsolutePath,
    string DisplayName,
    string ParentPromptContent,
    Func<CancellationToken, Task<string?>>? PlanContentLoader = null,
    string? PullRequestReference = null);

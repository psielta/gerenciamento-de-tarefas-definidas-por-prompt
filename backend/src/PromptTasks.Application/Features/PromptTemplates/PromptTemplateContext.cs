namespace PromptTasks.Application.Features.PromptTemplates;

public sealed record PromptTemplateContext(
    string AbsolutePath,
    string DisplayName,
    string ParentPromptContent,
    Func<CancellationToken, Task<string?>>? PlanContentLoader = null,
    string? PullRequestReference = null,
    IReadOnlyDictionary<string, string>? Inputs = null)
{
    public string? GetInputValue(string key)
    {
        if (Inputs is null)
        {
            return null;
        }

        return Inputs.TryGetValue(key, out var value) ? value : null;
    }
}

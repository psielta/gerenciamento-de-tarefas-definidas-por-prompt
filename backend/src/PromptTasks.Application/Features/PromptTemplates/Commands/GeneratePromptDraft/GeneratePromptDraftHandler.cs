using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.LinkedDocuments;

namespace PromptTasks.Application.Features.PromptTemplates.Commands.GeneratePromptDraft;

public sealed class GeneratePromptDraftHandler(
    IApplicationDbContext context,
    IPromptTemplateCatalog catalog,
    ICurrentUser currentUser)
    : IRequestHandler<GeneratePromptDraftCommand, GeneratedPromptDraftDto>
{
    public async Task<GeneratedPromptDraftDto> Handle(
        GeneratePromptDraftCommand request,
        CancellationToken cancellationToken)
    {
        var (document, prompt) = LinkedDocumentHelpers.GetDocument(
            context,
            request.LinkedDocumentId,
            currentUser.UserId);
        var template = catalog.Get(request.TemplateKey);
        var displayName = document.DisplayName ?? Path.GetFileName(document.AbsolutePath);
        var inputs = NormalizeInputs(request.Inputs, request.PullRequest);
        var templateContext = new PromptTemplateContext(
            document.AbsolutePath,
            displayName,
            prompt.Content,
            ct => LoadLatestPlanContentAsync(document.Id, ct),
            GetInputValue(inputs, "pullRequest"),
            inputs);
        var rendered = await template.RenderAsync(templateContext, cancellationToken);

        return new GeneratedPromptDraftDto(
            template.Key,
            document.Id,
            prompt.WorkingDirectoryId,
            prompt.Id,
            rendered.Title,
            rendered.Content,
            template.DefaultTargetAgent,
            template.DefaultKind);
    }

    private Task<string?> LoadLatestPlanContentAsync(Guid linkedDocumentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = context.LinkedDocumentVersions
            .Where(version => version.LinkedDocumentId == linkedDocumentId)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.Content)
            .FirstOrDefault();

        return Task.FromResult(content);
    }

    private static IReadOnlyDictionary<string, string> NormalizeInputs(
        IReadOnlyDictionary<string, string>? inputs,
        string? pullRequest)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (inputs is not null)
        {
            foreach (var (key, value) in inputs)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    normalized[key.Trim()] = value?.Trim() ?? string.Empty;
                }
            }
        }

        if (!normalized.ContainsKey("pullRequest") && pullRequest is not null)
        {
            normalized["pullRequest"] = pullRequest.Trim();
        }

        return normalized;
    }

    private static string? GetInputValue(IReadOnlyDictionary<string, string> inputs, string key) =>
        inputs.TryGetValue(key, out var value) ? value : null;
}

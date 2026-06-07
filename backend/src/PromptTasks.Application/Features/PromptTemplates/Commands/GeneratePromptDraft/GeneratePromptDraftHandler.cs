using FluentValidation;
using FluentValidation.Results;
using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.LinkedDocuments;
using PromptTasks.Domain.Prompts;

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
        var inputs = NormalizeInputs(request.Inputs, request.PullRequest, document.PullRequestReference);
        var pullRequest = GetInputValue(inputs, "pullRequest");
        EnsurePullRequestProvided(request.TemplateKey, pullRequest);
        var templateContext = new PromptTemplateContext(
            document.AbsolutePath,
            displayName,
            prompt.Content,
            ct => LoadLatestPlanContentAsync(document.Id, ct),
            pullRequest,
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
        string? pullRequest,
        string? fallbackPullRequest)
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

        // PR efetiva: input informado -> request.PullRequest -> valor salvo no plano.
        // Vazio/whitespace conta como ausente, garantindo o fallback do plano vinculado.
        var effectivePullRequest = FirstNonWhiteSpace(
            normalized.TryGetValue("pullRequest", out var inputPullRequest) ? inputPullRequest : null,
            pullRequest,
            fallbackPullRequest);

        if (effectivePullRequest is null)
        {
            normalized.Remove("pullRequest");
        }
        else
        {
            normalized["pullRequest"] = effectivePullRequest;
        }

        return normalized;
    }

    private static void EnsurePullRequestProvided(PromptTemplateKey templateKey, string? pullRequest)
    {
        var requiresPullRequest = templateKey is
            PromptTemplateKey.ReviewPullRequest or
            PromptTemplateKey.ReReviewPullRequest or
            PromptTemplateKey.MergePullRequest;

        if (requiresPullRequest && string.IsNullOrWhiteSpace(pullRequest))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    "pullRequest",
                    "Defina o numero da PR no plano vinculado ou informe na geracao."),
            });
        }
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? GetInputValue(IReadOnlyDictionary<string, string> inputs, string key) =>
        inputs.TryGetValue(key, out var value) ? value : null;
}

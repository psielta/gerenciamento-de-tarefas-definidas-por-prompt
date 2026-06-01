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
        var templateContext = new PromptTemplateContext(
            document.AbsolutePath,
            displayName,
            prompt.Content,
            ct => LoadLatestPlanContentAsync(document.Id, ct),
            request.PullRequest);
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
}

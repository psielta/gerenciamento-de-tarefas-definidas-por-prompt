using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.LinkedDocuments.Commands.SetLinkedDocumentPullRequest;

public sealed class SetLinkedDocumentPullRequestHandler(
    IApplicationDbContext context,
    ILinkedDocumentNotifier linkedDocumentNotifier,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetLinkedDocumentPullRequestCommand, LinkedDocumentDto>
{
    public async Task<LinkedDocumentDto> Handle(
        SetLinkedDocumentPullRequestCommand request,
        CancellationToken cancellationToken)
    {
        var (document, prompt) = LinkedDocumentHelpers.GetDocument(context, request.Id, currentUser.UserId);

        document.PullRequestReference = string.IsNullOrWhiteSpace(request.PullRequest)
            ? null
            : request.PullRequest.Trim();
        document.UpdatedAtUtc = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        var dto = document.ToDto();
        await linkedDocumentNotifier.LinkedDocumentUpdatedAsync(dto, prompt.WorkingDirectoryId, cancellationToken);
        return dto;
    }
}

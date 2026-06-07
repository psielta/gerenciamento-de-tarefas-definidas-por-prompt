using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.LinkedDocuments.Commands.SetLinkedDocumentPullRequest;

public sealed record SetLinkedDocumentPullRequestCommand(Guid Id, string? PullRequest) : IRequest<LinkedDocumentDto>;

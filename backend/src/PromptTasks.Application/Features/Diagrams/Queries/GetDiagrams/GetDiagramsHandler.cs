using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Features.Diagrams.Queries.GetDiagrams;

public sealed class GetDiagramsHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetDiagramsQuery, IReadOnlyList<DiagramSummaryDto>>
{
    public Task<IReadOnlyList<DiagramSummaryDto>> Handle(GetDiagramsQuery request, CancellationToken cancellationToken)
    {
        var diagrams = context.Diagrams
            .Where(diagram => diagram.OwnerId == currentUser.UserId
                && diagram.WorkingDirectoryId == request.WorkingDirectoryId);

        if (request.Type is { } type)
        {
            diagrams = diagrams.Where(diagram => diagram.Type == type);
        }

        if (!request.IncludeArchived)
        {
            diagrams = diagrams.Where(diagram => !diagram.IsArchived);
        }

        var term = request.Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(term))
        {
            diagrams = diagrams.Where(diagram =>
                diagram.Title.ToLower().Contains(term)
                || (diagram.Description != null && diagram.Description.ToLower().Contains(term))
                || (diagram.Type == DiagramType.Mermaid && diagram.Content.ToLower().Contains(term)));
        }

        // Project to the summary DTO at the query level so the (potentially large)
        // Content column is never loaded when listing diagrams.
        IReadOnlyList<DiagramSummaryDto> result = diagrams
            .OrderByDescending(diagram => diagram.UpdatedAtUtc)
            .Select(diagram => new DiagramSummaryDto(
                diagram.Id,
                diagram.WorkingDirectoryId,
                diagram.Title,
                diagram.Description,
                diagram.Type,
                diagram.IsArchived,
                diagram.CreatedAtUtc,
                diagram.UpdatedAtUtc))
            .ToList();

        return Task.FromResult(result);
    }
}

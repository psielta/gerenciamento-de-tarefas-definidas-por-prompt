using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Diagrams.Queries.GetDiagram;

public sealed class GetDiagramHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetDiagramQuery, DiagramDto>
{
    public Task<DiagramDto> Handle(GetDiagramQuery request, CancellationToken cancellationToken)
    {
        var diagram = context.Diagrams
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (diagram is null)
        {
            throw new NotFoundException("Diagram was not found.");
        }

        return Task.FromResult(diagram.ToDto());
    }
}

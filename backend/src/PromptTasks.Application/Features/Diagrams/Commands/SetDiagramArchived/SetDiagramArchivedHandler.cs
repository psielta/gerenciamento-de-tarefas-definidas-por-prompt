using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Diagrams.Commands.SetDiagramArchived;

public sealed class SetDiagramArchivedHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<SetDiagramArchivedCommand, DiagramDto>
{
    public async Task<DiagramDto> Handle(SetDiagramArchivedCommand request, CancellationToken cancellationToken)
    {
        var diagram = context.Diagrams
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (diagram is null)
        {
            throw new NotFoundException("Diagram was not found.");
        }

        diagram.IsArchived = request.IsArchived;
        await context.SaveChangesAsync(cancellationToken);

        return diagram.ToDto();
    }
}

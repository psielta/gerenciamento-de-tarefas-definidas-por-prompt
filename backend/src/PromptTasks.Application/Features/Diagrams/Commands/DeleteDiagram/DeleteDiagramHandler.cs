using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;

namespace PromptTasks.Application.Features.Diagrams.Commands.DeleteDiagram;

public sealed class DeleteDiagramHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<DeleteDiagramCommand>
{
    public async Task Handle(DeleteDiagramCommand request, CancellationToken cancellationToken)
    {
        var diagram = context.Diagrams
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (diagram is null)
        {
            throw new NotFoundException("Diagram was not found.");
        }

        context.Remove(diagram);
        await context.SaveChangesAsync(cancellationToken);
    }
}

using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Diagrams.Commands.UpdateDiagram;

public sealed class UpdateDiagramHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<UpdateDiagramCommand, DiagramDto>
{
    public async Task<DiagramDto> Handle(UpdateDiagramCommand request, CancellationToken cancellationToken)
    {
        var diagram = context.Diagrams
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (diagram is null)
        {
            throw new NotFoundException("Diagram was not found.");
        }

        diagram.Title = request.Title.Trim();
        diagram.Description = Normalize(request.Description);
        diagram.Content = request.Content ?? string.Empty;
        diagram.MetadataJson = Normalize(request.MetadataJson);

        await context.SaveChangesAsync(cancellationToken);

        return diagram.ToDto();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

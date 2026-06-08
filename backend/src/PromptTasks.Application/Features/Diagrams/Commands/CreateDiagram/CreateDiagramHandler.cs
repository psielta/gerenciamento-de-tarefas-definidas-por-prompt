using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Features.Diagrams.Commands.CreateDiagram;

public sealed class CreateDiagramHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<CreateDiagramCommand, DiagramDto>
{
    public async Task<DiagramDto> Handle(CreateDiagramCommand request, CancellationToken cancellationToken)
    {
        var workingDirectoryOwned = context.WorkingDirectories
            .Any(directory => directory.Id == request.WorkingDirectoryId && directory.OwnerId == currentUser.UserId);

        if (!workingDirectoryOwned)
        {
            throw new NotFoundException("Working directory was not found.");
        }

        var diagram = new Diagram
        {
            WorkingDirectoryId = request.WorkingDirectoryId,
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            Type = request.Type,
            Content = request.Content ?? string.Empty,
            MetadataJson = Normalize(request.MetadataJson),
            OwnerId = currentUser.UserId
        };

        context.Add(diagram);
        await context.SaveChangesAsync(cancellationToken);

        return diagram.ToDto();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

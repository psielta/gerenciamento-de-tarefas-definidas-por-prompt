using MediatR;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Features.Diagrams.Commands.CreateDiagram;

public sealed record CreateDiagramCommand(
    Guid WorkingDirectoryId,
    string Title,
    DiagramType Type,
    string? Description = null,
    string? Content = null,
    string? MetadataJson = null) : IRequest<DiagramDto>;

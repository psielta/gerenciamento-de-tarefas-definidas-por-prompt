using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Diagrams.Commands.UpdateDiagram;

public sealed record UpdateDiagramCommand(
    Guid Id,
    string Title,
    string Content,
    string? Description = null,
    string? MetadataJson = null) : IRequest<DiagramDto>;

using MediatR;

namespace PromptTasks.Application.Features.Diagrams.Commands.DeleteDiagram;

public sealed record DeleteDiagramCommand(Guid Id) : IRequest;

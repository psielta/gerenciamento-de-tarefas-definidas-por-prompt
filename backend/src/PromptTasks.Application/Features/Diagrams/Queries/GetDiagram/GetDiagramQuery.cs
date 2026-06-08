using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Diagrams.Queries.GetDiagram;

public sealed record GetDiagramQuery(Guid Id) : IRequest<DiagramDto>;

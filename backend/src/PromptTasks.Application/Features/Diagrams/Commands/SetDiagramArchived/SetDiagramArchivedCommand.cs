using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Diagrams.Commands.SetDiagramArchived;

public sealed record SetDiagramArchivedCommand(Guid Id, bool IsArchived) : IRequest<DiagramDto>;

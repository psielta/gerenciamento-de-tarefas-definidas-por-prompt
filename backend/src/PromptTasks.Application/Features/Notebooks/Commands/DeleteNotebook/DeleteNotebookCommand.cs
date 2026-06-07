using MediatR;

namespace PromptTasks.Application.Features.Notebooks.Commands.DeleteNotebook;

public sealed record DeleteNotebookCommand(Guid Id) : IRequest;

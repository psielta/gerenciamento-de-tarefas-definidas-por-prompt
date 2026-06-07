using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Commands.CreateNotebook;

public sealed record CreateNotebookCommand(
    string Title,
    string? Description = null,
    Guid? WorkingDirectoryId = null) : IRequest<NotebookDto>;

using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Commands.UpdateNotebook;

public sealed record UpdateNotebookCommand(
    Guid Id,
    string Title,
    string? Description = null,
    Guid? WorkingDirectoryId = null) : IRequest<NotebookDto>;

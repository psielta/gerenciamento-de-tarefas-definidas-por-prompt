using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Commands.SetNotebookArchived;

public sealed record SetNotebookArchivedCommand(Guid Id, bool IsArchived) : IRequest<NotebookDto>;

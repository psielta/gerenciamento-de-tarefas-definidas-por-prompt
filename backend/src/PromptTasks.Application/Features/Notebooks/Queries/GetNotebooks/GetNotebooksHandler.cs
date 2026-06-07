using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Queries.GetNotebooks;

public sealed class GetNotebooksHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetNotebooksQuery, IReadOnlyList<NotebookDto>>
{
    public Task<IReadOnlyList<NotebookDto>> Handle(GetNotebooksQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<NotebookDto> result = context.Notebooks
            .Where(notebook => notebook.OwnerId == currentUser.UserId)
            .Where(notebook => request.IncludeArchived || !notebook.IsArchived)
            .OrderBy(notebook => notebook.IsArchived)
            .ThenBy(notebook => notebook.Title)
            .Select(notebook => new NotebookDto(
                notebook.Id,
                notebook.Title,
                notebook.Description,
                notebook.WorkingDirectoryId,
                notebook.WorkingDirectory != null ? notebook.WorkingDirectory.Name : null,
                notebook.IsArchived,
                context.Notes.Count(note => note.NotebookId == notebook.Id && !note.IsArchived),
                notebook.CreatedAtUtc,
                notebook.UpdatedAtUtc))
            .ToList();

        return Task.FromResult(result);
    }
}

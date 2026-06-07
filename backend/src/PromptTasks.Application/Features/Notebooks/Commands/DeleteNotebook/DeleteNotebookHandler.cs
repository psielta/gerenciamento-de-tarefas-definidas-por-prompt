using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;

namespace PromptTasks.Application.Features.Notebooks.Commands.DeleteNotebook;

public sealed class DeleteNotebookHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<DeleteNotebookCommand>
{
    public async Task Handle(DeleteNotebookCommand request, CancellationToken cancellationToken)
    {
        var notebook = context.Notebooks
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (notebook is null)
        {
            throw new NotFoundException("Notebook was not found.");
        }

        var notes = context.Notes.Where(note => note.NotebookId == notebook.Id).ToList();
        if (notes.Count > 0)
        {
            context.RemoveRange(notes);
        }

        context.Remove(notebook);
        await context.SaveChangesAsync(cancellationToken);
    }
}

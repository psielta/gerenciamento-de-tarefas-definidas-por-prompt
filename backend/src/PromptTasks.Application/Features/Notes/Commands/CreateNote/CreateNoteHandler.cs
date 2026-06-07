using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Application.Features.Notes.Commands.CreateNote;

public sealed class CreateNoteHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<CreateNoteCommand, NoteDto>
{
    public async Task<NoteDto> Handle(CreateNoteCommand request, CancellationToken cancellationToken)
    {
        var notebookOwned = context.Notebooks
            .Any(notebook => notebook.Id == request.NotebookId && notebook.OwnerId == currentUser.UserId);

        if (!notebookOwned)
        {
            throw new NotFoundException("Notebook was not found.");
        }

        var note = new Note
        {
            NotebookId = request.NotebookId,
            Title = request.Title.Trim(),
            ContentMarkdown = request.ContentMarkdown ?? string.Empty,
            OwnerId = currentUser.UserId
        };

        context.Add(note);
        await context.SaveChangesAsync(cancellationToken);

        return note.ToDto();
    }
}

using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Commands.SetNoteArchived;

public sealed class SetNoteArchivedHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<SetNoteArchivedCommand, NoteDto>
{
    public async Task<NoteDto> Handle(SetNoteArchivedCommand request, CancellationToken cancellationToken)
    {
        var note = context.Notes
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (note is null)
        {
            throw new NotFoundException("Note was not found.");
        }

        note.IsArchived = request.IsArchived;
        await context.SaveChangesAsync(cancellationToken);

        return note.ToDto();
    }
}

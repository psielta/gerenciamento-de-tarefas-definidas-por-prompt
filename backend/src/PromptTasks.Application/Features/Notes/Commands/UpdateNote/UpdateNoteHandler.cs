using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Commands.UpdateNote;

public sealed class UpdateNoteHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<UpdateNoteCommand, NoteDto>
{
    public async Task<NoteDto> Handle(UpdateNoteCommand request, CancellationToken cancellationToken)
    {
        var note = context.Notes
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (note is null)
        {
            throw new NotFoundException("Note was not found.");
        }

        note.Title = request.Title.Trim();
        note.ContentMarkdown = request.ContentMarkdown ?? string.Empty;

        await context.SaveChangesAsync(cancellationToken);

        return note.ToDto();
    }
}

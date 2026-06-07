using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;

namespace PromptTasks.Application.Features.Notes.Commands.DeleteNote;

public sealed class DeleteNoteHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<DeleteNoteCommand>
{
    public async Task Handle(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var note = context.Notes
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (note is null)
        {
            throw new NotFoundException("Note was not found.");
        }

        context.Remove(note);
        await context.SaveChangesAsync(cancellationToken);
    }
}

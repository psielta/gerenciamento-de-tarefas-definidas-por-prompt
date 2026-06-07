using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Queries.GetNote;

public sealed class GetNoteHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetNoteQuery, NoteDto>
{
    public Task<NoteDto> Handle(GetNoteQuery request, CancellationToken cancellationToken)
    {
        var note = context.Notes
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (note is null)
        {
            throw new NotFoundException("Note was not found.");
        }

        return Task.FromResult(note.ToDto());
    }
}

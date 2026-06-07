using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Commands.SetNoteArchived;

public sealed record SetNoteArchivedCommand(Guid Id, bool IsArchived) : IRequest<NoteDto>;

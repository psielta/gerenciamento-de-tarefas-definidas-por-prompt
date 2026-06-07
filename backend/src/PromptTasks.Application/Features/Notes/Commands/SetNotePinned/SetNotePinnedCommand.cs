using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Commands.SetNotePinned;

public sealed record SetNotePinnedCommand(Guid Id, bool IsPinned) : IRequest<NoteDto>;

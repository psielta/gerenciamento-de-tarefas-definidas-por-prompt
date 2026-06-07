using MediatR;

namespace PromptTasks.Application.Features.Notes.Commands.DeleteNote;

public sealed record DeleteNoteCommand(Guid Id) : IRequest;

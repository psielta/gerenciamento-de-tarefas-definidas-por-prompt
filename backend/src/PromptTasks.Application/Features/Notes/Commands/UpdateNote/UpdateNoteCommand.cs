using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Commands.UpdateNote;

public sealed record UpdateNoteCommand(
    Guid Id,
    string Title,
    string ContentMarkdown) : IRequest<NoteDto>;

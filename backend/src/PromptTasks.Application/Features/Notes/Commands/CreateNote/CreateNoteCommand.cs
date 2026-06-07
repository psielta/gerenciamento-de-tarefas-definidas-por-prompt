using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Commands.CreateNote;

public sealed record CreateNoteCommand(
    Guid NotebookId,
    string Title,
    string? ContentMarkdown = null) : IRequest<NoteDto>;

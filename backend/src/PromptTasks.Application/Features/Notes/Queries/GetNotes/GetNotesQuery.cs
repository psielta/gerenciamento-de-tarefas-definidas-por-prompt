using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notes.Queries.GetNotes;

public sealed record GetNotesQuery(
    Guid? NotebookId = null,
    string? Search = null,
    bool IncludeArchived = false) : IRequest<IReadOnlyList<NoteDto>>;

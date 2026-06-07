namespace PromptTasks.Application.Common.Models;

public sealed record NoteDto(
    Guid Id,
    Guid NotebookId,
    string Title,
    string ContentMarkdown,
    bool IsPinned,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

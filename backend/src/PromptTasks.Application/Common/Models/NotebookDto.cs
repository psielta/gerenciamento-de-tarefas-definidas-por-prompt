namespace PromptTasks.Application.Common.Models;

public sealed record NotebookDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? WorkingDirectoryId,
    string? WorkingDirectoryName,
    bool IsArchived,
    int NoteCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

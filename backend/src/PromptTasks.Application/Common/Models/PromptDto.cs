using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Common.Models;

public sealed record PromptDto(
    Guid Id,
    Guid WorkingDirectoryId,
    Guid? ParentPromptId,
    string? TaskNumber,
    string Title,
    string Content,
    TargetAgent TargetAgent,
    PromptKind Kind,
    PromptStatus Status,
    int CurrentVersion,
    string RowVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<FileMentionDto> Mentions);

using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Common.Models;

public sealed record LinkedDocumentDto(
    Guid Id,
    Guid PromptId,
    Guid? WorkingDirectoryId,
    string AbsolutePath,
    string DisplayName,
    LinkedDocumentType DocumentType,
    LinkedDocumentStatus Status,
    string? PullRequestReference,
    int CurrentVersion,
    string? LastContentHash,
    long? SizeBytes,
    string? LastError,
    DateTimeOffset? LastSyncedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

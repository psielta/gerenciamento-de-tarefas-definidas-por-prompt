using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Application.Common.Models;

public sealed record DiagramSummaryDto(
    Guid Id,
    Guid WorkingDirectoryId,
    string Title,
    string? Description,
    DiagramType Type,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

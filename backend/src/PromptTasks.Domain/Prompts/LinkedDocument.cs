using PromptTasks.Domain.Common;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Domain.Prompts;

public sealed class LinkedDocument : Entity
{
    public Guid PromptId { get; set; }
    public Guid? WorkingDirectoryId { get; set; }
    public string AbsolutePath { get; set; } = string.Empty;
    public string AbsolutePathKey { get; set; } = string.Empty;
    public LinkedDocumentType DocumentType { get; set; } = LinkedDocumentType.ClaudeCodePlan;
    public string? DisplayName { get; set; }
    public LinkedDocumentStatus Status { get; set; } = LinkedDocumentStatus.Draft;
    public string? PullRequestReference { get; set; }
    public int CurrentVersion { get; set; }
    public string? LastContentHash { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastSyncedAtUtc { get; set; }
    public long? SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Prompt? Prompt { get; set; }
    public WorkingDirectory? WorkingDirectory { get; set; }
    public ICollection<LinkedDocumentVersion> Versions { get; } = new List<LinkedDocumentVersion>();
}

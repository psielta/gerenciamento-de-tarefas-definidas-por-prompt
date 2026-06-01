using PromptTasks.Domain.Common;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Domain.Prompts;

public sealed class Prompt : AuditableEntity
{
    public Guid WorkingDirectoryId { get; set; }
    public Guid? ParentPromptId { get; set; }
    public string? TaskNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public TargetAgent TargetAgent { get; set; } = TargetAgent.ClaudeCode;
    public PromptKind Kind { get; set; } = PromptKind.General;
    public PromptStatus Status { get; set; } = PromptStatus.Draft;
    public int CurrentVersion { get; set; } = 1;
    public uint RowVersion { get; private set; }

    public WorkingDirectory? WorkingDirectory { get; set; }
    public Prompt? ParentPrompt { get; set; }
    public User? Owner { get; set; }
    public ICollection<Prompt> ChildPrompts { get; } = new List<Prompt>();
    public ICollection<PromptVersion> Versions { get; } = new List<PromptVersion>();
    public ICollection<PromptFileReference> FileReferences { get; } = new List<PromptFileReference>();
    public ICollection<LinkedDocument> LinkedDocuments { get; } = new List<LinkedDocument>();
}

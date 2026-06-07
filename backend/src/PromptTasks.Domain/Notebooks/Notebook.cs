using PromptTasks.Domain.Common;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Domain.Notebooks;

/// <summary>
/// A user-owned collection of Markdown notes. Notebooks live entirely in the
/// database and may optionally be linked to a working directory.
/// </summary>
public sealed class Notebook : AuditableEntity
{
    public const int MaxTitleLength = 160;
    public const int MaxDescriptionLength = 500;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? WorkingDirectoryId { get; set; }
    public bool IsArchived { get; set; }

    public User? Owner { get; set; }
    public WorkingDirectory? WorkingDirectory { get; set; }
    public ICollection<Note> Notes { get; } = new List<Note>();
}

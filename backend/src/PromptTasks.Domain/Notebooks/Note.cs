using PromptTasks.Domain.Common;
using PromptTasks.Domain.Users;

namespace PromptTasks.Domain.Notebooks;

/// <summary>
/// A single Markdown note that belongs to a <see cref="Notebook"/>. Notes are
/// persisted in the database and never written as files on disk.
/// </summary>
public sealed class Note : AuditableEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 100_000;

    public Guid NotebookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }

    public User? Owner { get; set; }
    public Notebook? Notebook { get; set; }
}

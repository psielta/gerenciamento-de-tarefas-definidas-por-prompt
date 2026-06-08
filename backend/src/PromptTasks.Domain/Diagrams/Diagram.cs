using PromptTasks.Domain.Common;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Domain.Diagrams;

/// <summary>
/// A user-owned diagram that belongs to a working directory. Diagrams live
/// entirely in the database and are never written as files on disk. The
/// <see cref="Type"/> distinguishes an Excalidraw scene (JSON in
/// <see cref="Content"/>) from Mermaid source (text in <see cref="Content"/>).
/// </summary>
public sealed class Diagram : AuditableEntity
{
    public const int MaxTitleLength = 160;
    public const int MaxDescriptionLength = 500;
    public const int MaxContentLength = 2_000_000;
    public const int MaxMetadataLength = 8_000;

    public Guid WorkingDirectoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiagramType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public bool IsArchived { get; set; }

    public User? Owner { get; set; }
    public WorkingDirectory? WorkingDirectory { get; set; }
}

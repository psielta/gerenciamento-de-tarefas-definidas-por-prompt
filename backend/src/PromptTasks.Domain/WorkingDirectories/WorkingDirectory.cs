using PromptTasks.Domain.Common;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;

namespace PromptTasks.Domain.WorkingDirectories;

public sealed class WorkingDirectory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string AbsolutePath { get; set; } = string.Empty;
    public bool RespectGitignore { get; set; } = true;
    public bool EnableAiContext { get; set; }

    public User? Owner { get; set; }
    public ICollection<Prompt> Prompts { get; } = new List<Prompt>();
}

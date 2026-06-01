using PromptTasks.Domain.Common;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Domain.Prompts;

public sealed class DailyTaskSequence : Entity
{
    public Guid WorkingDirectoryId { get; set; }
    public DateOnly SequenceDate { get; set; }
    public int CurrentValue { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public WorkingDirectory? WorkingDirectory { get; set; }
}

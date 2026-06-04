using PromptTasks.Domain.Common;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Domain.Workflows;

public sealed class PromptWorkflow : Entity
{
    public Guid PromptId { get; set; }
    public PromptWorkflowStatus Status { get; set; } = PromptWorkflowStatus.Active;
    public Guid? CurrentPhaseId { get; set; }
    public string? CurrentPhaseName { get; set; }
    public string? CurrentPhaseColor { get; set; }
    public WorkflowActor? CurrentActor { get; set; }
    public int CurrentPhaseIteration { get; set; } = 1;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EnteredCurrentPhaseAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public uint RowVersion { get; private set; }

    public Prompt? Prompt { get; set; }
    public ICollection<PromptWorkflowPhase> Phases { get; } = new List<PromptWorkflowPhase>();
    public ICollection<PromptWorkflowEvent> Events { get; } = new List<PromptWorkflowEvent>();
}

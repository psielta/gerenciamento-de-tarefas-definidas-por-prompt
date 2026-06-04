using PromptTasks.Domain.Common;

namespace PromptTasks.Domain.Workflows;

public sealed class PromptWorkflowPhase : Entity
{
    public Guid PromptWorkflowId { get; set; }
    public string Name { get; set; } = string.Empty;
    public WorkflowActor DefaultActor { get; set; }
    public int OrderIndex { get; set; }
    public string Color { get; set; } = string.Empty;
    public WorkflowPhaseRole? Role { get; set; }

    public PromptWorkflow? Workflow { get; set; }
}

using PromptTasks.Domain.Common;

namespace PromptTasks.Domain.Workflows;

public sealed class WorkflowTemplatePhase : Entity
{
    public Guid WorkflowTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public WorkflowActor DefaultActor { get; set; }
    public int OrderIndex { get; set; }
    public string Color { get; set; } = string.Empty;
    public WorkflowPhaseRole? Role { get; set; }

    public WorkflowTemplate? Template { get; set; }
}

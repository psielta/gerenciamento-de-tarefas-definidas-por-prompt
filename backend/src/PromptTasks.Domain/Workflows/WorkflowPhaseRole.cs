namespace PromptTasks.Domain.Workflows;

public enum WorkflowPhaseRole
{
    PromptEngineering = 1,
    Planning = 2,
    PlanReview = 3,
    PlanCorrection = 4,
    Implementation = 5,
    CodeReview = 6,
    ReviewCorrection = 7,
    PracticalTest = 8,
    Rebase = 9,
    Merge = 10
}

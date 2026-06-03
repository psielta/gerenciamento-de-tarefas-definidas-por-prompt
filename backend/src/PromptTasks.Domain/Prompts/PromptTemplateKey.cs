namespace PromptTasks.Domain.Prompts;

public enum PromptTemplateKey
{
    ReviewPlan = 1,
    ImplementPlan = 2,
    ReviewPlanWithParentPrompt = 3,
    ReReviewPlan = 4,
    ImplementPlanInWorktree = 5,
    ReviewPullRequest = 6,
    MergePullRequest = 7,
    RebaseCurrentBranch = 8,
    ReReviewPullRequest = 9
}

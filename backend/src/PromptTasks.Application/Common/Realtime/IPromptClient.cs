using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Common.Realtime;

public interface IPromptClient
{
    Task PromptCreated(PromptDto prompt);
    Task PromptUpdated(PromptDto prompt);
    Task PromptDeleted(Guid promptId, Guid workingDirectoryId);
    Task LinkedDocumentLinked(LinkedDocumentDto document);
    Task LinkedDocumentUpdated(LinkedDocumentDto document);
    Task LinkedDocumentRemoved(Guid linkedDocumentId, Guid promptId, Guid workingDirectoryId);
    Task TaskWorkflowChanged(TaskSummaryDto summary);
    Task AgentUsageUpdated(AgentUsageDto usage);
}

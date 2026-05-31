using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Common.Interfaces;

public interface IAgentUsageNotifier
{
    Task AgentUsageUpdatedAsync(AgentUsageDto usage, CancellationToken cancellationToken);
}

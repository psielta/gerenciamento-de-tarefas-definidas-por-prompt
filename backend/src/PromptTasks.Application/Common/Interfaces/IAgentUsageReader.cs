using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Common.Interfaces;

public interface IAgentUsageReader
{
    Task<AgentUsageDto> ReadAsync(CancellationToken cancellationToken);
}

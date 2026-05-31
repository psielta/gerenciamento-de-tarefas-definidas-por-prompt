using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Common.Interfaces;

public interface ICodexUsageReader
{
    Task<AgentUsageInfo> ReadAsync(CancellationToken cancellationToken);
}

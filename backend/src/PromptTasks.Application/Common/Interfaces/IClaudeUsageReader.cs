using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Common.Interfaces;

public interface IClaudeUsageReader
{
    Task<AgentUsageInfo> ReadAsync(CancellationToken cancellationToken);
}

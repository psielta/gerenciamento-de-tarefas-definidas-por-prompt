using Microsoft.AspNetCore.SignalR;
using PromptTasks.Api.Hubs;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Common.Realtime;

namespace PromptTasks.Api.Realtime;

public sealed class SignalRAgentUsageNotifier(IHubContext<PromptHub, IPromptClient> hubContext) : IAgentUsageNotifier
{
    public Task AgentUsageUpdatedAsync(AgentUsageDto usage, CancellationToken cancellationToken) =>
        hubContext.Clients.All.AgentUsageUpdated(usage);
}

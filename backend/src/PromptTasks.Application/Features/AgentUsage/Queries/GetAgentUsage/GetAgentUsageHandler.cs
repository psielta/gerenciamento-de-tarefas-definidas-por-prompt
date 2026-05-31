using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.AgentUsage.Queries.GetAgentUsage;

public sealed class GetAgentUsageHandler(IAgentUsageReader reader) : IRequestHandler<GetAgentUsageQuery, AgentUsageDto>
{
    public Task<AgentUsageDto> Handle(GetAgentUsageQuery request, CancellationToken cancellationToken) =>
        reader.ReadAsync(cancellationToken);
}

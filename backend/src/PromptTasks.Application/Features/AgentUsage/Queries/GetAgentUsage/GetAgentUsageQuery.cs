using MediatR;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.AgentUsage.Queries.GetAgentUsage;

public sealed record GetAgentUsageQuery : IRequest<AgentUsageDto>;

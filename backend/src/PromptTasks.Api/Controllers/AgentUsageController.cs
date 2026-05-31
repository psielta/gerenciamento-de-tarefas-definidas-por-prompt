using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.AgentUsage.Queries.GetAgentUsage;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class AgentUsageController(ISender sender) : ControllerBase
{
    [HttpGet("agent-usage")]
    public async Task<ActionResult<AgentUsageDto>> Get(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAgentUsageQuery(), cancellationToken));
}

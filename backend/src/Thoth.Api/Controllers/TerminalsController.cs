using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Thoth.Api.Common;
using Thoth.Application.Common.Models;
using Thoth.Application.Features.Terminals;
using Thoth.Application.Features.Terminals.Commands.CloseTerminalSession;
using Thoth.Application.Features.Terminals.Commands.CreateTerminalSession;
using Thoth.Application.Features.Terminals.Queries.ListTerminalSessions;
using Thoth.Infrastructure.Terminals;

namespace Thoth.Api.Controllers;

[ApiController]
public sealed class TerminalsController(ISender sender, IOptions<TerminalOptions> terminalOptions) : ControllerBase
{
    [HttpGet("api/terminals/capabilities")]
    public ActionResult<TerminalCapabilitiesResponse> GetCapabilities()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        return Ok(new TerminalCapabilitiesResponse(TerminalAccessGuard.IsEnabledFor(terminalOptions, remoteIp)));
    }

    [HttpPost("api/prompts/{promptId:guid}/terminals")]
    public async Task<ActionResult<TerminalSessionDescriptor>> Create(
        Guid promptId,
        CreateTerminalRequest request,
        CancellationToken cancellationToken)
    {
        TerminalAccessGuard.EnsureAccess(terminalOptions, HttpContext.Connection.RemoteIpAddress);
        TerminalAgentLaunch? agentLaunch = null;
        if (!string.IsNullOrWhiteSpace(request.AgentLaunch))
        {
            if (!TerminalAgentLaunchCommands.TryParse(request.AgentLaunch, out var parsed))
            {
                return BadRequest($"Agent launch '{request.AgentLaunch}' is not supported.");
            }

            agentLaunch = parsed;
        }

        var result = await sender.Send(
            new CreateTerminalSessionCommand(promptId, request.Shell, agentLaunch),
            cancellationToken);
        return CreatedAtAction(nameof(ListForPrompt), new { promptId }, result);
    }

    [HttpGet("api/prompts/{promptId:guid}/terminals")]
    public async Task<ActionResult<IReadOnlyList<TerminalSessionDescriptor>>> ListForPrompt(
        Guid promptId,
        CancellationToken cancellationToken)
    {
        TerminalAccessGuard.EnsureAccess(terminalOptions, HttpContext.Connection.RemoteIpAddress);
        return Ok(await sender.Send(new ListTerminalSessionsQuery(promptId), cancellationToken));
    }

    [HttpDelete("api/terminals/{sessionId:guid}")]
    public async Task<IActionResult> Close(Guid sessionId, CancellationToken cancellationToken)
    {
        TerminalAccessGuard.EnsureAccess(terminalOptions, HttpContext.Connection.RemoteIpAddress);
        await sender.Send(new CloseTerminalSessionCommand(sessionId), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateTerminalRequest(string? Shell, string? AgentLaunch);

public sealed record TerminalCapabilitiesResponse(bool Enabled);
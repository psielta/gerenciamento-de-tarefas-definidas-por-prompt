using MediatR;
using Thoth.Application.Common.Models;
using Thoth.Application.Features.Terminals;

namespace Thoth.Application.Features.Terminals.Commands.CreateTerminalSession;

public sealed record CreateTerminalSessionCommand(
    Guid PromptId,
    string? Shell,
    TerminalAgentLaunch? AgentLaunch) : IRequest<TerminalSessionDescriptor>;
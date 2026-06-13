using MediatR;
using Thoth.Application.Common.Exceptions;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;
using Thoth.Application.Features.Prompts;
using Thoth.Application.Features.Terminals;
using Thoth.Domain.Prompts;

namespace Thoth.Application.Features.Terminals.Commands.CreateTerminalSession;

public sealed class CreateTerminalSessionHandler(
    IApplicationDbContext context,
    ICurrentUser currentUser,
    ITerminalSessionCoordinator terminalCoordinator)
    : IRequestHandler<CreateTerminalSessionCommand, TerminalSessionDescriptor>
{
    public async Task<TerminalSessionDescriptor> Handle(
        CreateTerminalSessionCommand request,
        CancellationToken cancellationToken)
    {
        var prompt = PromptMutationHelpers.GetPrompt(context, request.PromptId, currentUser.UserId);
        if (prompt.Status == PromptStatus.Archived)
        {
            throw new ForbiddenException("Cannot create terminal sessions for archived prompts.");
        }

        var directory = ResolveWorkspaceDirectory(context, prompt, currentUser.UserId);

        var initialInput = TerminalAgentLaunchCommands.ResolveInitialInput(request.AgentLaunch);

        return await terminalCoordinator.CreateAsync(
            prompt.Id,
            directory.AbsolutePath,
            request.Shell ?? string.Empty,
            initialInput,
            cancellationToken);
    }

    private static Domain.WorkingDirectories.WorkingDirectory ResolveWorkspaceDirectory(
        IApplicationDbContext context,
        Prompt prompt,
        Guid ownerId)
    {
        if (prompt.ParentPromptId is { } parentPromptId)
        {
            var parentPrompt = PromptMutationHelpers.GetPrompt(context, parentPromptId, ownerId);
            return PromptMutationHelpers.GetWorkingDirectory(context, parentPrompt.WorkingDirectoryId, ownerId);
        }

        return PromptMutationHelpers.GetWorkingDirectory(context, prompt.WorkingDirectoryId, ownerId);
    }
}
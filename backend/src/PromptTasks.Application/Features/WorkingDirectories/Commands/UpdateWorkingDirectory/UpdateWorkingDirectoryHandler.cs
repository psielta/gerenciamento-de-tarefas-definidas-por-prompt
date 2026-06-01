using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.WorkingDirectories.Commands.UpdateWorkingDirectory;

public sealed class UpdateWorkingDirectoryHandler(
    IApplicationDbContext context,
    IWorkspaceFileService workspaceFileService,
    ICurrentUser currentUser,
    IGeminiClient gemini)
    : IRequestHandler<UpdateWorkingDirectoryCommand, WorkingDirectoryDto>
{
    public async Task<WorkingDirectoryDto> Handle(UpdateWorkingDirectoryCommand request, CancellationToken cancellationToken)
    {
        var directory = context.WorkingDirectories
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (directory is null)
        {
            throw new NotFoundException("Working directory was not found.");
        }

        var path = await workspaceFileService.ValidatePathAsync(request.AbsolutePath, cancellationToken);
        if (!path.IsValid || path.CanonicalPath is null)
        {
            throw new PathTraversalException(path.Error ?? "Invalid working directory path.");
        }

        if (context.WorkingDirectories
            .Where(item => item.Id != request.Id && item.OwnerId == currentUser.UserId)
            .AsEnumerable()
            .Any(item => item.AbsolutePath.Equals(path.CanonicalPath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("This working directory is already registered.");
        }

        var contextFlagChanged = directory.EnableAiContext != request.EnableAiContext;
        var pathChanged = !directory.AbsolutePath.Equals(path.CanonicalPath, StringComparison.OrdinalIgnoreCase);

        directory.Name = request.Name.Trim();
        directory.AbsolutePath = path.CanonicalPath;
        directory.RespectGitignore = request.RespectGitignore;
        directory.EnableAiContext = request.EnableAiContext;
        if (request.TaskNumberPattern is not null)
        {
            directory.TaskNumberPattern = NormalizePattern(request.TaskNumberPattern);
        }

        if (contextFlagChanged || (request.EnableAiContext && pathChanged))
        {
            var sessions = context.AiChatSessions
                .Where(session => session.WorkingDirectoryId == directory.Id
                                  && session.OwnerId == currentUser.UserId
                                  && session.GeminiCacheName != null)
                .ToList();

            foreach (var session in sessions)
            {
                var cacheName = session.GeminiCacheName;
                session.GeminiCacheName = null;
                session.CacheSystemInstructionHash = null;
                session.CacheExpiresAt = null;
                session.CachedThroughSequence = 0;

                if (cacheName is not null)
                {
                    try { await gemini.DeleteCacheAsync(cacheName, cancellationToken); }
                    catch { /* best effort */ }
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return directory.ToDto();
    }

    private static string? NormalizePattern(string pattern) =>
        string.IsNullOrWhiteSpace(pattern) ? null : pattern.Trim();
}

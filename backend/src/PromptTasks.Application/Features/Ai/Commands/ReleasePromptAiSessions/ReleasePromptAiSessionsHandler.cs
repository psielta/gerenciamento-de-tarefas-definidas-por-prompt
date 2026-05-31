using MediatR;
using PromptTasks.Application.Common.Interfaces;

namespace PromptTasks.Application.Features.Ai.Commands.ReleasePromptAiSessions;

public sealed class ReleasePromptAiSessionsHandler(
    IApplicationDbContext context,
    IGeminiClient gemini)
    : IRequestHandler<ReleasePromptAiSessionsCommand>
{
    public async Task Handle(ReleasePromptAiSessionsCommand request, CancellationToken cancellationToken)
    {
        var sessions = context.AiChatSessions
            .Where(s => s.PromptId == request.PromptId && s.GeminiCacheName != null)
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

        if (sessions.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
    }
}

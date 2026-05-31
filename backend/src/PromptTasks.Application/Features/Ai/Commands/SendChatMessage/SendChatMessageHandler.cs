using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;
using PromptTasks.Domain.Ai;

namespace PromptTasks.Application.Features.Ai.Commands.SendChatMessage;

public sealed class SendChatMessageHandler(
    IApplicationDbContext context,
    IGeminiClient gemini,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IWorkspaceFileService workspaceFiles)
    : IStreamRequestHandler<SendChatMessageCommand, ChatChunkDto>
{
    // Used as inline fallback when system cache is unavailable
    private const string FallbackSystemInstruction =
        "Você é um assistente especializado em engenharia de prompts para Claude Code e Codex. " +
        "SEMPRE formate suas respostas em Markdown: use cabeçalhos, listas, negrito, itálico, " +
        "código com indicação de linguagem (```csharp, ```typescript, etc.) e tabelas quando adequado. " +
        "Para blocos de código, sempre especifique a linguagem. Seja claro, direto e técnico.";
    public async IAsyncEnumerable<ChatChunkDto> Handle(
        SendChatMessageCommand request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var session = context.AiChatSessions
            .FirstOrDefault(s => s.Id == request.SessionId && s.OwnerId == currentUser.UserId)
            ?? throw new NotFoundException($"Sessão {request.SessionId} não encontrada.");

        if (string.IsNullOrWhiteSpace(request.Message))
            throw new FluentValidation.ValidationException("Mensagem não pode ser vazia.");

        var now = dateTimeProvider.UtcNow;

        var existingMessages = context.AiChatMessages
            .Where(m => m.SessionId == session.Id)
            .OrderBy(m => m.Sequence)
            .ToList();

        var nextSequence = existingMessages.Count > 0 ? existingMessages.Max(m => m.Sequence) + 1 : 1;

        // Auto-title from first user message
        if (existingMessages.Count == 0)
        {
            var raw = request.Message.Trim();
            session.Title = raw.Length <= 60 ? raw : raw[..57] + "...";
        }

        var userContent = request.IncludePromptContext && !string.IsNullOrWhiteSpace(request.PromptContent)
            ? $"{request.Message}\n\n---\n**Conteúdo do prompt atual:**\n{request.PromptContent}"
            : request.Message;

        string? workspaceContext = null;
        if (session.WorkingDirectoryId is { } workingDirectoryId)
        {
            var workspace = context.WorkingDirectories
                .FirstOrDefault(directory =>
                    directory.Id == workingDirectoryId && directory.OwnerId == currentUser.UserId);

            if (workspace is { EnableAiContext: true })
            {
                workspaceContext = await workspaceFiles.ReadWorkspaceContextAsync(
                    workspace.AbsolutePath,
                    cancellationToken);
            }
        }

        var systemInstruction = string.IsNullOrWhiteSpace(workspaceContext)
            ? FallbackSystemInstruction
            : $"{FallbackSystemInstruction}\n\n{workspaceContext}";
        var systemInstructionHash = HashSystemInstruction(systemInstruction);

        var userMessage = new AiChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = userContent,
            Sequence = nextSequence,
            CreatedAtUtc = now,
        };
        context.Add(userMessage);
        await context.SaveChangesAsync(cancellationToken);

        var cacheValid = session.GeminiCacheName is not null &&
                         session.CacheExpiresAt.HasValue &&
                         session.CacheExpiresAt.Value > now.AddMinutes(2) &&
                         session.CacheSystemInstructionHash == systemInstructionHash;

        IReadOnlyList<GeminiTurn> contents;
        string? cachedContentName = null;

        if (cacheValid)
        {
            contents = existingMessages
                .Where(m => m.Sequence > session.CachedThroughSequence)
                .Select(m => new GeminiTurn(m.Role, m.Content))
                .Append(new GeminiTurn("user", userContent))
                .ToList();
            cachedContentName = session.GeminiCacheName;
        }
        else
        {
            contents = existingMessages
                .Select(m => new GeminiTurn(m.Role, m.Content))
                .Append(new GeminiTurn("user", userContent))
                .ToList();
        }

        var thinkingMode = session.ThinkingEnabled
            ? (session.ThinkingBudget.HasValue ? "budget" : (session.ThinkingLevel is not null ? "level" : "none"))
            : "none";

        var thinking = new GeminiThinking(
            Mode: thinkingMode,
            Budget: session.ThinkingEnabled ? session.ThinkingBudget : 0,
            Level: session.ThinkingEnabled ? session.ThinkingLevel : null);

        var geminiRequest = new GeminiGenerationRequest(
            Model: session.Model,
            Temperature: session.Temperature,
            Thinking: thinking,
            IncludeThoughts: true,
            UseSystemCache: !cacheValid && string.IsNullOrWhiteSpace(workspaceContext),
            CachedContentName: cachedContentName,
            SystemInstruction: systemInstruction,
            Contents: contents);

        var responseText = new StringBuilder();
        int? lastCachedTokens = null;

        await foreach (var chunk in gemini.StreamAsync(geminiRequest, cancellationToken))
        {
            if (!chunk.IsThought && !string.IsNullOrEmpty(chunk.Text))
                responseText.Append(chunk.Text);
            if (chunk.CachedTokens.HasValue)
                lastCachedTokens = chunk.CachedTokens;

            yield return new ChatChunkDto(chunk.Text, chunk.IsThought, chunk.Done, chunk.CachedTokens);
        }

        var modelText = responseText.ToString();
        var modelSequence = nextSequence + 1;

        var modelMessage = new AiChatMessage
        {
            SessionId = session.Id,
            Role = "model",
            Content = modelText,
            Sequence = modelSequence,
            CachedTokens = lastCachedTokens,
            CreatedAtUtc = dateTimeProvider.UtcNow,
        };
        context.Add(modelMessage);

        var allMessages = existingMessages
            .Append(new AiChatMessage { Role = "user", Content = userContent, Sequence = nextSequence })
            .Append(new AiChatMessage { Role = "model", Content = modelText, Sequence = modelSequence })
            .ToList();

        var estimatedNewTokens = allMessages
            .Where(m => !cacheValid || m.Sequence > session.CachedThroughSequence)
            .Sum(m => m.Content.Length / 4);

        if (!cacheValid || estimatedNewTokens > 4096)
        {
            var history = allMessages
                .Select(m => new GeminiTurn(m.Role, m.Content))
                .ToList();

            var cacheHandle = await gemini.EnsureSessionCacheAsync(session.Model, systemInstruction, history, cancellationToken);
            if (cacheHandle is not null)
            {
                session.GeminiCacheName = cacheHandle.Name;
                session.CacheExpiresAt = cacheHandle.ExpiresAt;
                session.CachedThroughSequence = modelSequence;
                session.CacheSystemInstructionHash = systemInstructionHash;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string HashSystemInstruction(string systemInstruction)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(systemInstruction));
        return Convert.ToHexString(bytes);
    }
}

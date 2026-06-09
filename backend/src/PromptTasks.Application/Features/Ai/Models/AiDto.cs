namespace PromptTasks.Application.Features.Ai.Models;

public sealed record RefinedPromptDto(string Content, int PromptTokens, int CandidateTokens);

public sealed record GeneratedNoteDto(
    string? SuggestedTitle,
    string ContentMarkdown,
    int PromptTokens,
    int CandidateTokens);

public sealed record GeneratedMermaidDto(
    string MermaidCode,
    string? TitleSuggestion,
    int PromptTokens,
    int CandidateTokens,
    IReadOnlyList<string> Warnings);

public sealed record GeminiModelDto(
    string Id,
    string Label,
    string ThinkingMode,
    bool CanDisableThinking,
    int ThinkingBudgetMin,
    int ThinkingBudgetMax,
    int MinCacheTokens);

public sealed record AiSettingsDto(
    string Model,
    double Temperature,
    bool ThinkingEnabled,
    int? ThinkingBudget,
    string? ThinkingLevel);

public sealed record AiChatSessionDto(
    Guid Id,
    Guid? WorkingDirectoryId,
    Guid? PromptId,
    string Title,
    string Model,
    double Temperature,
    bool ThinkingEnabled,
    int? ThinkingBudget,
    string? ThinkingLevel,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<AiChatMessageDto> Messages);

public sealed record AiChatMessageDto(
    Guid Id,
    string Role,
    string Content,
    int Sequence,
    int? CachedTokens,
    DateTimeOffset CreatedAtUtc);

public sealed record ChatChunkDto(
    string Text,
    bool IsThought,
    bool Done,
    int? CachedTokens);

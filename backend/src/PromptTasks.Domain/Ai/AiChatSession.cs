using PromptTasks.Domain.Common;

namespace PromptTasks.Domain.Ai;

public sealed class AiChatSession : AuditableEntity
{
    public Guid? WorkingDirectoryId { get; set; }
    public Guid? PromptId { get; set; }
    public string Title { get; set; } = "";
    public string Model { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public bool ThinkingEnabled { get; set; }
    public int? ThinkingBudget { get; set; }
    public string? ThinkingLevel { get; set; }
    public string? GeminiCacheName { get; set; }
    public string? CacheSystemInstructionHash { get; set; }
    public DateTimeOffset? CacheExpiresAt { get; set; }
    public int CachedThroughSequence { get; set; }
    public ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
}

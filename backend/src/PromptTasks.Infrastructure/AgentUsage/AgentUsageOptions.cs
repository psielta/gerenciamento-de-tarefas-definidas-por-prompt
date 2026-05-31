namespace PromptTasks.Infrastructure.AgentUsage;

public sealed class AgentUsageOptions
{
    public bool Enabled { get; set; } = true;
    public int ReconcileSeconds { get; set; } = 60;
    public int DebounceMilliseconds { get; set; } = 500;
    public ClaudeUsageOptions Claude { get; set; } = new();
    public CodexUsageOptions Codex { get; set; } = new();
}

public sealed class ClaudeUsageOptions
{
    public string? CredentialsPath { get; set; }
    public string UsageEndpoint { get; set; } = "https://api.anthropic.com/api/oauth/usage";
    public string AnthropicBetaHeader { get; set; } = "oauth-2025-04-20";
    public string UserAgent { get; set; } = "claude-code/unknown";
    public int RequestTimeoutSeconds { get; set; } = 10;
    public int CacheTtlSeconds { get; set; } = 60;
    public bool EnableTranscriptFallback { get; set; }
    public string? ProjectsDir { get; set; }
    public Dictionary<string, AgentUsageTierBudget> TierBudgets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CodexUsageOptions
{
    public string? SessionsDir { get; set; }
    public int MaxFilesToScan { get; set; } = 5;
    public int CacheTtlSeconds { get; set; } = 30;
}

public sealed class AgentUsageTierBudget
{
    public long FiveHourTokens { get; set; }
    public long WeeklyTokens { get; set; }
}

namespace PromptTasks.Application.Common.Models;

public sealed record AgentUsageDto(
    DateTimeOffset CapturedAtUtc,
    AgentUsageInfo Claude,
    AgentUsageInfo Codex);

public sealed record AgentUsageInfo(
    string Agent,
    AgentUsageStatus Status,
    int? HttpStatusCode,
    string? StatusDetail,
    string? Plan,
    IReadOnlyList<AgentUsageWindow> Windows)
{
    public static AgentUsageInfo Disabled(string agent) =>
        new(agent, AgentUsageStatus.Disabled, null, "Agent usage monitoring is disabled.", null, Array.Empty<AgentUsageWindow>());
}

public sealed record AgentUsageWindow(
    string Key,
    string Label,
    double UsedPercent,
    DateTimeOffset? ResetsAtUtc,
    int? WindowMinutes,
    bool Estimated,
    long? UsedTokens,
    long? LimitTokens);

public enum AgentUsageStatus
{
    Ok = 1,
    NoToken = 2,
    Unauthorized = 3,
    RateLimited = 4,
    HttpError = 5,
    Timeout = 6,
    NetworkError = 7,
    NoData = 8,
    Disabled = 9,
    Unavailable = 10
}

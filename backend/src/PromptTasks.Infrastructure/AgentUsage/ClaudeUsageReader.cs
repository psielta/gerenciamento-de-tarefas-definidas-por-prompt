using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Infrastructure.AgentUsage;

public sealed class ClaudeUsageReader(
    HttpClient httpClient,
    IOptions<AgentUsageOptions> options,
    IMemoryCache cache,
    IDateTimeProvider dateTimeProvider)
    : IClaudeUsageReader
{
    private const string CacheKey = "agent-usage:claude";

    public async Task<AgentUsageInfo> ReadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out AgentUsageInfo? cached) && cached is not null)
        {
            return cached;
        }

        var result = await ReadFreshAsync(cancellationToken);
        cache.Set(CacheKey, result, TimeSpan.FromSeconds(Math.Max(options.Value.Claude.CacheTtlSeconds, 1)));
        return result;
    }

    private async Task<AgentUsageInfo> ReadFreshAsync(CancellationToken cancellationToken)
    {
        var credentials = ReadCredentials(cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return new AgentUsageInfo(
                "Claude",
                AgentUsageStatus.NoToken,
                null,
                "Claude credentials were not found.",
                credentials.Plan,
                Array.Empty<AgentUsageWindow>());
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, options.Value.Claude.UsageEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            request.Headers.TryAddWithoutValidation("anthropic-beta", options.Value.Claude.AnthropicBetaHeader);
            request.Headers.UserAgent.TryParseAdd(options.Value.Claude.UserAgent);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await CreateHttpErrorAsync(response, credentials, cancellationToken);
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var windows = ParseUsageWindows(content);
            if (windows.Count == 0)
            {
                return new AgentUsageInfo(
                    "Claude",
                    AgentUsageStatus.NoData,
                    null,
                    "Claude usage response did not contain usage windows.",
                    credentials.Plan,
                    Array.Empty<AgentUsageWindow>());
            }

            return new AgentUsageInfo("Claude", AgentUsageStatus.Ok, null, null, credentials.Plan, windows);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await TryFallbackAsync(
                AgentUsageStatus.Timeout,
                null,
                "Claude usage request timed out.",
                credentials,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return await TryFallbackAsync(
                AgentUsageStatus.NetworkError,
                null,
                AgentUsageText.Sanitize(exception.Message, credentials.AccessToken),
                credentials,
                cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            return await TryFallbackAsync(
                AgentUsageStatus.Unavailable,
                null,
                AgentUsageText.Sanitize(exception.Message, credentials.AccessToken),
                credentials,
                cancellationToken);
        }
    }

    private async Task<AgentUsageInfo> CreateHttpErrorAsync(
        HttpResponseMessage response,
        ClaudeCredentials credentials,
        CancellationToken cancellationToken)
    {
        var status = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AgentUsageStatus.Unauthorized,
            HttpStatusCode.TooManyRequests => AgentUsageStatus.RateLimited,
            _ => AgentUsageStatus.HttpError
        };

        var detail = await ReadHttpDetailAsync(response, credentials.AccessToken, cancellationToken);
        return await TryFallbackAsync(status, (int)response.StatusCode, detail, credentials, cancellationToken);
    }

    private async Task<AgentUsageInfo> TryFallbackAsync(
        AgentUsageStatus status,
        int? httpStatusCode,
        string? detail,
        ClaudeCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (options.Value.Claude.EnableTranscriptFallback)
        {
            var fallback = await TryReadTranscriptFallbackAsync(credentials.Plan, cancellationToken);
            if (fallback is not null)
            {
                return fallback;
            }
        }

        return new AgentUsageInfo("Claude", status, httpStatusCode, detail, credentials.Plan, Array.Empty<AgentUsageWindow>());
    }

    private ClaudeCredentials ReadCredentials(CancellationToken cancellationToken)
    {
        var path = ResolveCredentialsPath();
        if (path is null || !File.Exists(path))
        {
            return new ClaudeCredentials(null, null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = OpenReadShared(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var json = reader.ReadToEnd();
            var root = JObject.Parse(json);
            var oauth = root["claudeAiOauth"];
            var token = oauth?.Value<string>("accessToken");
            var subscriptionType = oauth?.Value<string>("subscriptionType");
            var rateLimitTier = oauth?.Value<string>("rateLimitTier");
            var plan = !string.IsNullOrWhiteSpace(subscriptionType)
                ? subscriptionType
                : rateLimitTier;

            return new ClaudeCredentials(token, plan);
        }
        catch (Exception exception) when (exception is IOException
                                            or UnauthorizedAccessException
                                            or Newtonsoft.Json.JsonException)
        {
            return new ClaudeCredentials(null, null);
        }
    }

    private string? ResolveCredentialsPath()
    {
        var configured = options.Value.Claude.CredentialsPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ExpandUserPath(configured);
        }

        var environmentPath = Environment.GetEnvironmentVariable("CLAUDE_CREDENTIALS_PATH");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return ExpandUserPath(environmentPath);
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
        {
            return null;
        }

        var primary = Path.Combine(profile, ".claude", ".credentials.json");
        if (File.Exists(primary))
        {
            return primary;
        }

        return Path.Combine(profile, ".config", "claude", ".credentials.json");
    }

    private List<AgentUsageWindow> ParseUsageWindows(string content)
    {
        var root = JObject.Parse(content);
        var windows = new List<AgentUsageWindow>();
        AddWindow(windows, root, "five_hour", "Sessao 5h", 300);
        AddWindow(windows, root, "seven_day", "Semana", 10080);
        AddWindow(windows, root, "seven_day_opus", "Semana Opus", 10080);
        return windows;
    }

    private static void AddWindow(
        ICollection<AgentUsageWindow> windows,
        JObject root,
        string key,
        string label,
        int windowMinutes)
    {
        if (root[key] is not JObject source)
        {
            return;
        }

        var utilization = source.Value<double?>("utilization") ?? 0;
        var resetsAtRaw = source.Value<string>("resets_at");
        DateTimeOffset? resetsAt = null;
        if (!string.IsNullOrWhiteSpace(resetsAtRaw) &&
            DateTimeOffset.TryParse(resetsAtRaw, out var parsedReset))
        {
            resetsAt = parsedReset.ToUniversalTime();
        }

        windows.Add(new AgentUsageWindow(
            key,
            label,
            AgentUsageText.ClampPercent(utilization),
            resetsAt,
            windowMinutes,
            false,
            null,
            null));
    }

    private async Task<string?> ReadHttpDetailAsync(
        HttpResponseMessage response,
        string? secret,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusLine = $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim();
            if (string.IsNullOrWhiteSpace(body))
            {
                return AgentUsageText.Sanitize(statusLine, secret);
            }

            return AgentUsageText.Sanitize($"{statusLine}: {body}", secret);
        }
        catch
        {
            return AgentUsageText.Sanitize($"{(int)response.StatusCode} {response.ReasonPhrase}", secret);
        }
    }

    private async Task<AgentUsageInfo?> TryReadTranscriptFallbackAsync(string? plan, CancellationToken cancellationToken)
    {
        var tier = plan ?? "default";
        if (!options.Value.Claude.TierBudgets.TryGetValue(tier, out var budget) ||
            budget.FiveHourTokens <= 0 ||
            budget.WeeklyTokens <= 0)
        {
            return null;
        }

        var projectsDir = ResolveProjectsDir();
        if (projectsDir is null || !Directory.Exists(projectsDir))
        {
            return null;
        }

        var now = dateTimeProvider.UtcNow;
        var fiveHourStart = now.AddHours(-5);
        var weeklyStart = now.AddDays(-7);
        var seen = new Dictionary<string, (DateTimeOffset Timestamp, long Tokens)>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(projectsDir, "*.jsonl", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            if (info.LastWriteTimeUtc < weeklyStart.UtcDateTime)
            {
                continue;
            }

            foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
            {
                TryReadClaudeTranscriptLine(line, seen);
            }
        }

        var fiveHourTokens = seen.Values.Where(item => item.Timestamp >= fiveHourStart).Sum(item => item.Tokens);
        var weeklyTokens = seen.Values.Where(item => item.Timestamp >= weeklyStart).Sum(item => item.Tokens);
        var windows = new[]
        {
            new AgentUsageWindow(
                "five_hour",
                "Sessao 5h",
                AgentUsageText.ClampPercent(100d * fiveHourTokens / budget.FiveHourTokens),
                EstimateReset(seen.Values, fiveHourStart, TimeSpan.FromHours(5)),
                300,
                true,
                fiveHourTokens,
                budget.FiveHourTokens),
            new AgentUsageWindow(
                "seven_day",
                "Semana",
                AgentUsageText.ClampPercent(100d * weeklyTokens / budget.WeeklyTokens),
                EstimateReset(seen.Values, weeklyStart, TimeSpan.FromDays(7)),
                10080,
                true,
                weeklyTokens,
                budget.WeeklyTokens)
        };

        return new AgentUsageInfo("Claude", AgentUsageStatus.Ok, null, "Estimated from local transcripts.", plan, windows);
    }

    private static void TryReadClaudeTranscriptLine(
        string line,
        IDictionary<string, (DateTimeOffset Timestamp, long Tokens)> seen)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            var root = JObject.Parse(line);
            if (!string.Equals(root.Value<string>("type"), "assistant", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!DateTimeOffset.TryParse(root.Value<string>("timestamp"), out var timestamp))
            {
                return;
            }

            var message = root["message"] as JObject;
            var id = message?.Value<string>("id");
            var usage = message?["usage"] as JObject;
            if (string.IsNullOrWhiteSpace(id) || usage is null)
            {
                return;
            }

            var tokens =
                (usage.Value<long?>("input_tokens") ?? 0) +
                (usage.Value<long?>("output_tokens") ?? 0) +
                (usage.Value<long?>("cache_creation_input_tokens") ?? 0) +
                (usage.Value<long?>("cache_read_input_tokens") ?? 0);

            if (!seen.TryGetValue(id, out var existing) || tokens > existing.Tokens)
            {
                seen[id] = (timestamp.ToUniversalTime(), tokens);
            }
        }
        catch (Newtonsoft.Json.JsonException)
        {
        }
    }

    private static DateTimeOffset? EstimateReset(
        IEnumerable<(DateTimeOffset Timestamp, long Tokens)> messages,
        DateTimeOffset windowStart,
        TimeSpan window)
    {
        var oldest = messages
            .Where(item => item.Timestamp >= windowStart)
            .Select(item => (DateTimeOffset?)item.Timestamp)
            .Min();

        return oldest?.Add(window);
    }

    private string? ResolveProjectsDir()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Claude.ProjectsDir))
        {
            return ExpandUserPath(options.Value.Claude.ProjectsDir);
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(profile) ? null : Path.Combine(profile, ".claude", "projects");
    }

    private static string ExpandUserPath(string path)
    {
        if (path.StartsWith("~", StringComparison.Ordinal))
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(profile, path[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return Path.GetFullPath(path);
    }

    private static FileStream OpenReadShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private sealed record ClaudeCredentials(string? AccessToken, string? Plan);
}

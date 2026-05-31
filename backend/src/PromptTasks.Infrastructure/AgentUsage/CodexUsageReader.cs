using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Infrastructure.AgentUsage;

public sealed class CodexUsageReader(
    IOptions<AgentUsageOptions> options,
    IMemoryCache cache,
    IDateTimeProvider dateTimeProvider)
    : ICodexUsageReader
{
    private const string CacheKey = "agent-usage:codex";

    public async Task<AgentUsageInfo> ReadAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out AgentUsageInfo? cached) && cached is not null)
        {
            return cached;
        }

        var result = await ReadFreshAsync(cancellationToken);
        cache.Set(CacheKey, result, TimeSpan.FromSeconds(Math.Max(options.Value.Codex.CacheTtlSeconds, 1)));
        return result;
    }

    private async Task<AgentUsageInfo> ReadFreshAsync(CancellationToken cancellationToken)
    {
        var sessionsDir = ResolveSessionsDir();
        if (sessionsDir is null || !Directory.Exists(sessionsDir))
        {
            return new AgentUsageInfo("Codex", AgentUsageStatus.NoData, null, "Codex sessions directory was not found.", null, Array.Empty<AgentUsageWindow>());
        }

        try
        {
            var files = Directory
                .EnumerateFiles(sessionsDir, "rollout-*.jsonl", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(Math.Max(options.Value.Codex.MaxFilesToScan, 1))
                .ToList();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = await ExtractSnapshotAsync(file.FullName, cancellationToken);
                if (snapshot is not null)
                {
                    return snapshot;
                }
            }

            return new AgentUsageInfo("Codex", AgentUsageStatus.NoData, null, "No active Codex rate limit snapshot was found.", null, Array.Empty<AgentUsageWindow>());
        }
        catch (Exception exception) when (exception is IOException
                                            or UnauthorizedAccessException
                                            or ArgumentException
                                            or NotSupportedException)
        {
            return new AgentUsageInfo(
                "Codex",
                AgentUsageStatus.Unavailable,
                null,
                AgentUsageText.Sanitize(exception.Message),
                null,
                Array.Empty<AgentUsageWindow>());
        }
    }

    private async Task<AgentUsageInfo?> ExtractSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = OpenReadShared(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var lines = content.Split('\n');

        for (var index = lines.Length - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                var root = JObject.Parse(line);
                var payload = GetTokenCountPayload(root);
                var rateLimits = payload?["rate_limits"] ?? payload?["info"]?["rate_limits"];
                if (rateLimits is not JObject rateLimitsObject)
                {
                    continue;
                }

                var snapshot = CreateSnapshot(rateLimitsObject);
                if (snapshot is not null)
                {
                    return snapshot;
                }
            }
            catch (Newtonsoft.Json.JsonException)
            {
            }
        }

        return null;
    }

    private AgentUsageInfo? CreateSnapshot(JObject rateLimits)
    {
        var nowSeconds = dateTimeProvider.UtcNow.ToUnixTimeSeconds();
        var windows = new List<AgentUsageWindow>();

        AddWindow(windows, rateLimits["primary"] as JObject, "primary", "Sessao 5h", 300, nowSeconds);
        AddWindow(windows, rateLimits["secondary"] as JObject, "secondary", "Semana", 10080, nowSeconds);

        if (windows.Count == 0)
        {
            return null;
        }

        var plan = rateLimits.Value<string>("plan_type") ?? rateLimits.Value<string>("limit_id");
        return new AgentUsageInfo("Codex", AgentUsageStatus.Ok, null, null, plan, windows);
    }

    private static JObject? GetTokenCountPayload(JObject root)
    {
        if (string.Equals(root.Value<string>("type"), "event_msg", StringComparison.OrdinalIgnoreCase))
        {
            var payload = root["payload"] as JObject;
            return string.Equals(payload?.Value<string>("type"), "token_count", StringComparison.OrdinalIgnoreCase)
                ? payload
                : null;
        }

        return string.Equals(root.Value<string>("type"), "token_count", StringComparison.OrdinalIgnoreCase)
            ? root
            : null;
    }

    private static void AddWindow(
        ICollection<AgentUsageWindow> windows,
        JObject? source,
        string key,
        string label,
        int defaultWindowMinutes,
        long nowSeconds)
    {
        if (source is null || !IsActive(source, nowSeconds))
        {
            return;
        }

        var resetsAtSeconds = source.Value<long?>("resets_at");
        var resetsAt = resetsAtSeconds.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(resetsAtSeconds.Value)
            : (DateTimeOffset?)null;

        windows.Add(new AgentUsageWindow(
            key,
            label,
            AgentUsageText.ClampPercent(source.Value<double?>("used_percent") ?? 0),
            resetsAt,
            source.Value<int?>("window_minutes") ?? defaultWindowMinutes,
            false,
            null,
            null));
    }

    private static bool IsActive(JObject source, long nowSeconds)
    {
        var resetsAt = source.Value<long?>("resets_at");
        return !resetsAt.HasValue || resetsAt.Value > nowSeconds;
    }

    private string? ResolveSessionsDir()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.Codex.SessionsDir))
        {
            return ExpandUserPath(options.Value.Codex.SessionsDir);
        }

        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            return Path.Combine(ExpandUserPath(codexHome), "sessions");
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(profile) ? null : Path.Combine(profile, ".codex", "sessions");
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
}

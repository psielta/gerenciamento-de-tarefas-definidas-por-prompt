using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PromptTasks.Application.Common.Interfaces;

namespace PromptTasks.Infrastructure.AgentUsage;

public sealed class AgentUsageRefreshService(
    IServiceScopeFactory scopeFactory,
    IOptions<AgentUsageOptions> options,
    ILogger<AgentUsageRefreshService> logger)
    : BackgroundService
{
    private readonly object _debounceGate = new();
    private Timer? _debounceTimer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Agent usage monitoring is disabled.");
            return;
        }

        using var watcher = CreateCodexWatcher();
        await PushSafeAsync(stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(options.Value.ReconcileSeconds, 30));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PushSafeAsync(stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_debounceGate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        return base.StopAsync(cancellationToken);
    }

    private FileSystemWatcher? CreateCodexWatcher()
    {
        var sessionsDir = ResolveCodexSessionsDir();
        if (sessionsDir is null || !Directory.Exists(sessionsDir))
        {
            return null;
        }

        try
        {
            var watcher = new FileSystemWatcher(sessionsDir, "*.jsonl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            watcher.Changed += (_, _) => SchedulePush();
            watcher.Created += (_, _) => SchedulePush();
            watcher.Deleted += (_, _) => SchedulePush();
            watcher.Renamed += (_, _) => SchedulePush();
            watcher.Error += (_, args) => logger.LogWarning(args.GetException(), "Codex usage watcher failed.");
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception exception) when (exception is IOException
                                            or UnauthorizedAccessException
                                            or ArgumentException
                                            or NotSupportedException)
        {
            logger.LogWarning(exception, "Codex usage watcher could not be started.");
            return null;
        }
    }

    private void SchedulePush()
    {
        var dueTime = TimeSpan.FromMilliseconds(Math.Max(options.Value.DebounceMilliseconds, 100));
        lock (_debounceGate)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => _ = PushSafeAsync(CancellationToken.None),
                null,
                dueTime,
                Timeout.InfiniteTimeSpan);
        }
    }

    private async Task PushSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IAgentUsageReader>();
            var notifier = scope.ServiceProvider.GetRequiredService<IAgentUsageNotifier>();
            var usage = await reader.ReadAsync(cancellationToken);
            await notifier.AgentUsageUpdatedAsync(usage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Agent usage refresh failed.");
        }
    }

    private string? ResolveCodexSessionsDir()
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
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thoth.Application.Common.Exceptions;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;
using Thoth.Infrastructure.FileSystem;

namespace Thoth.Infrastructure.Terminals;

public sealed class TerminalSessionManager(
    IServiceScopeFactory scopeFactory,
    IPtyConnectionFactory ptyConnectionFactory,
    IOptions<TerminalOptions> options,
    ILogger<TerminalSessionManager> logger)
    : BackgroundService, ITerminalSessionCoordinator, IDisposable
{
    private readonly TerminalOptions _options = options.Value;
    private readonly Channel<TerminalOutputChunk> _outputQueue = Channel.CreateBounded<TerminalOutputChunk>(
        new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
    private readonly ConcurrentDictionary<Guid, TerminalSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _sessionsByPrompt = new();
    private readonly ConcurrentDictionary<string, HashSet<Guid>> _sessionsByConnection = new(StringComparer.Ordinal);

    public async Task<TerminalSessionDescriptor> CreateAsync(
        Guid promptId,
        string cwd,
        string shell,
        byte[]? initialInput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            throw new ForbiddenException("Terminal sessions are disabled.");
        }

        string canonicalCwd;
        try
        {
            canonicalCwd = WorkspaceFilePath.CanonicalizeExistingPath(cwd);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new NotFoundException("Working directory path is inaccessible.");
        }

        if (!Directory.Exists(canonicalCwd))
        {
            throw new NotFoundException("Working directory path does not exist.");
        }

        var resolvedShell = ResolveShell(shell);
        EnsureCapacity(promptId);

        var sessionId = Guid.CreateVersion7();
        IPtyConnection pty;
        try
        {
            pty = await ptyConnectionFactory.CreateAsync(
                resolvedShell,
                canonicalCwd,
                cols: 120,
                rows: 30,
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.InnerException is System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(exception, "Failed to start shell {Shell}", resolvedShell);
            throw new NotFoundException($"Failed to start shell '{resolvedShell}'.");
        }

        var session = new TerminalSession
        {
            Id = sessionId,
            PromptId = promptId,
            Shell = resolvedShell,
            Cwd = canonicalCwd,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Pty = pty,
            LastActivityUtc = DateTimeOffset.UtcNow
        };

        pty.Exited += (_, exitCode) => HandlePtyExited(sessionId, exitCode);

        if (!_sessions.TryAdd(sessionId, session))
        {
            await pty.DisposeAsync();
            throw new ConflictException("Failed to register terminal session.");
        }

        var promptSessions = _sessionsByPrompt.GetOrAdd(promptId, _ => new HashSet<Guid>());
        lock (promptSessions)
        {
            promptSessions.Add(sessionId);
        }

        _ = PumpOutputAsync(session);

        if (initialInput is { Length: > 0 })
        {
            _ = DeliverInitialInputAsync(session, initialInput);
        }

        logger.LogInformation(
            "Terminal session {SessionId} created for prompt {PromptId} shell {Shell} cwd {Cwd}",
            sessionId,
            promptId,
            resolvedShell,
            canonicalCwd);

        return ToDescriptor(session);
    }

    public void WriteInput(Guid sessionId, byte[] input)
    {
        if (input.Length == 0)
        {
            return;
        }

        if (input.Length > _options.MaxInputBytes)
        {
            input = input[.._options.MaxInputBytes];
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        session.LastActivityUtc = DateTimeOffset.UtcNow;
        try
        {
            session.Pty.WriterStream.Write(input, 0, input.Length);
            session.Pty.WriterStream.Flush();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to write input to terminal session {SessionId}", sessionId);
        }
    }

    public void Resize(Guid sessionId, ushort cols, ushort rows)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        session.LastActivityUtc = DateTimeOffset.UtcNow;
        try
        {
            session.Pty.Resize(cols, rows);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to resize terminal session {SessionId}", sessionId);
        }
    }

    public Task CloseAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KillSession(sessionId, "closed");
        return Task.CompletedTask;
    }

    public void AttachConnection(Guid sessionId, string connectionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        lock (session.Gate)
        {
            session.Connections.Add(connectionId);
        }

        session.LastActivityUtc = DateTimeOffset.UtcNow;

        var connectionSessions = _sessionsByConnection.GetOrAdd(connectionId, _ => new HashSet<Guid>());
        lock (connectionSessions)
        {
            connectionSessions.Add(sessionId);
        }
    }

    public void DetachConnection(Guid sessionId, string connectionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var becameOrphaned = false;
            lock (session.Gate)
            {
                session.Connections.Remove(connectionId);
                becameOrphaned = session.Connections.Count == 0;
            }

            if (becameOrphaned)
            {
                session.LastActivityUtc = DateTimeOffset.UtcNow;
            }
        }

        if (_sessionsByConnection.TryGetValue(connectionId, out var connectionSessions))
        {
            lock (connectionSessions)
            {
                connectionSessions.Remove(sessionId);
                if (connectionSessions.Count == 0)
                {
                    _sessionsByConnection.TryRemove(connectionId, out _);
                }
            }
        }
    }

    public void ReleaseConnection(string connectionId)
    {
        if (!_sessionsByConnection.TryRemove(connectionId, out var sessionIds))
        {
            return;
        }

        List<Guid> ids;
        lock (sessionIds)
        {
            ids = sessionIds.ToList();
        }

        foreach (var sessionId in ids)
        {
            DetachConnection(sessionId, connectionId);
        }
    }

    public IReadOnlyList<TerminalSessionDescriptor> ListForPrompt(Guid promptId)
    {
        if (!_sessionsByPrompt.TryGetValue(promptId, out var sessionIds))
        {
            return Array.Empty<TerminalSessionDescriptor>();
        }

        List<Guid> ids;
        lock (sessionIds)
        {
            ids = sessionIds.ToList();
        }

        return ids
            .Select(id => _sessions.TryGetValue(id, out var session) ? ToDescriptor(session) : null)
            .Where(descriptor => descriptor is not null)
            .Cast<TerminalSessionDescriptor>()
            .OrderBy(descriptor => descriptor.CreatedAtUtc)
            .ToList();
    }

    public TerminalSessionDescriptor? TryGetSession(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? ToDescriptor(session) : null;

    public Task KillForPromptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_sessionsByPrompt.TryRemove(promptId, out var sessionIds))
        {
            return Task.CompletedTask;
        }

        List<Guid> ids;
        lock (sessionIds)
        {
            ids = sessionIds.ToList();
        }

        foreach (var sessionId in ids)
        {
            KillSession(sessionId, "prompt-killed");
        }

        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reaper = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(_options.OrphanTimeoutSeconds / 4, 1, 15)));

        await Task.WhenAll(
            ProcessOutputQueueAsync(stoppingToken),
            ReapOrphansAsync(reaper, stoppingToken));
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var sessionId in _sessions.Keys.ToList())
        {
            KillSession(sessionId, "shutdown");
        }

        _outputQueue.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.FlushTimer?.Dispose();
            session.OutputPumpCts.Cancel();
            session.OutputPumpCts.Dispose();
            session.Pty.Kill();
            _ = session.Pty.DisposeAsync();
        }

        _sessions.Clear();
        _sessionsByPrompt.Clear();
        _sessionsByConnection.Clear();
        base.Dispose();
    }

    private async Task ProcessOutputQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var chunk in _outputQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var notifier = scope.ServiceProvider.GetRequiredService<ITerminalNotifier>();
                await notifier.TerminalOutputAsync(
                    chunk.SessionId,
                    Convert.ToBase64String(chunk.Data),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to notify terminal output for session {SessionId}",
                    chunk.SessionId);
            }
        }
    }

    private async Task ReapOrphansAsync(PeriodicTimer reaper, CancellationToken stoppingToken)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(_options.OrphanTimeoutSeconds, 30));

        while (await reaper.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var (sessionId, session) in _sessions.ToArray())
            {
                int connectionCount;
                lock (session.Gate)
                {
                    connectionCount = session.Connections.Count;
                }

                if (connectionCount == 0 && now - session.LastActivityUtc > timeout)
                {
                    KillSession(sessionId, "orphan-reaped");
                }
            }
        }
    }

    private async Task DeliverInitialInputAsync(TerminalSession session, byte[] input)
    {
        try
        {
            await Task.Delay(500);
            if (!_sessions.ContainsKey(session.Id))
            {
                return;
            }

            session.LastActivityUtc = DateTimeOffset.UtcNow;
            session.Pty.WriterStream.Write(input, 0, input.Length);
            session.Pty.WriterStream.Flush();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to deliver initial input to terminal session {SessionId}",
                session.Id);
        }
    }

    private async Task PumpOutputAsync(TerminalSession session)
    {
        var buffer = new byte[4096];
        var token = session.OutputPumpCts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                var bytesRead = await session.Pty.ReaderStream.ReadAsync(buffer, token);
                if (bytesRead <= 0)
                {
                    break;
                }

                lock (session.Gate)
                {
                    session.OutputBuffer.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
                }

                ScheduleFlush(session);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Terminal output pump failed for session {SessionId}", session.Id);
        }
    }

    private void ScheduleFlush(TerminalSession session)
    {
        var shouldFlushImmediately = false;
        lock (session.Gate)
        {
            shouldFlushImmediately = session.OutputBuffer.Count >= _options.MaxOutputChunkBytes;
        }

        if (shouldFlushImmediately)
        {
            FlushOutput(session);
            return;
        }

        if (session.FlushTimer is not null)
        {
            return;
        }

        var dueTime = TimeSpan.FromMilliseconds(Math.Max(_options.OutputFlushMilliseconds, 10));
        session.FlushTimer = new Timer(_ =>
        {
            session.FlushTimer?.Dispose();
            session.FlushTimer = null;
            FlushOutput(session);
        }, null, dueTime, Timeout.InfiniteTimeSpan);
    }

    private void FlushOutput(TerminalSession session)
    {
        byte[] data;
        lock (session.Gate)
        {
            if (session.OutputBuffer.Count == 0)
            {
                return;
            }

            var length = Math.Min(session.OutputBuffer.Count, _options.MaxOutputChunkBytes);
            data = session.OutputBuffer.GetRange(0, length).ToArray();
            session.OutputBuffer.RemoveRange(0, length);
        }

        if (!_outputQueue.Writer.TryWrite(new TerminalOutputChunk(session.Id, data)))
        {
            lock (session.Gate)
            {
                session.OutputBuffer.InsertRange(0, data);
            }

            ScheduleFlush(session);
            return;
        }

        lock (session.Gate)
        {
            if (session.OutputBuffer.Count > 0)
            {
                ScheduleFlush(session);
            }
        }
    }

    private void HandlePtyExited(Guid sessionId, int exitCode)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return;
        }

        try
        {
            session.OutputPumpCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        session.FlushTimer?.Dispose();

        FlushOutput(session);
        RemoveSessionIndexes(session);

        _ = NotifyExitAsync(sessionId, exitCode);
        _ = session.Pty.DisposeAsync();

        logger.LogInformation("Terminal session {SessionId} exited with code {ExitCode}", sessionId, exitCode);
    }

    private void KillSession(Guid sessionId, string reason)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return;
        }

        try
        {
            session.OutputPumpCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        session.FlushTimer?.Dispose();
        RemoveSessionIndexes(session);

        try
        {
            KillProcessTree(session.Pty.ProcessId);
            session.Pty.Kill();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to kill terminal session {SessionId} ({Reason})", sessionId, reason);
        }

        FlushOutput(session);
        _ = NotifyExitAsync(sessionId, -1);
        _ = session.Pty.DisposeAsync();
        logger.LogInformation("Terminal session {SessionId} killed ({Reason})", sessionId, reason);
    }

    private void RemoveSessionIndexes(TerminalSession session)
    {
        if (_sessionsByPrompt.TryGetValue(session.PromptId, out var promptSessions))
        {
            lock (promptSessions)
            {
                promptSessions.Remove(session.Id);
                if (promptSessions.Count == 0)
                {
                    _sessionsByPrompt.TryRemove(session.PromptId, out _);
                }
            }
        }

        List<string> connectionIds;
        lock (session.Gate)
        {
            connectionIds = session.Connections.ToList();
            session.Connections.Clear();
        }

        foreach (var connectionId in connectionIds)
        {
            if (_sessionsByConnection.TryGetValue(connectionId, out var connectionSessions))
            {
                lock (connectionSessions)
                {
                    connectionSessions.Remove(session.Id);
                    if (connectionSessions.Count == 0)
                    {
                        _sessionsByConnection.TryRemove(connectionId, out _);
                    }
                }
            }
        }
    }

    private async Task NotifyExitAsync(Guid sessionId, int exitCode)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var notifier = scope.ServiceProvider.GetRequiredService<ITerminalNotifier>();
            await notifier.TerminalExitedAsync(sessionId, exitCode, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to notify terminal exit for session {SessionId}", sessionId);
        }
    }

    private void EnsureCapacity(Guid promptId)
    {
        if (_sessions.Count >= _options.MaxTotalSessions)
        {
            throw new ForbiddenException("Maximum total terminal sessions reached.");
        }

        if (_sessionsByPrompt.TryGetValue(promptId, out var promptSessions))
        {
            int count;
            lock (promptSessions)
            {
                count = promptSessions.Count;
            }

            if (count >= _options.MaxSessionsPerPrompt)
            {
                throw new ForbiddenException("Maximum terminal sessions per prompt reached.");
            }
        }
    }

    private string ResolveShell(string shell)
    {
        var candidate = string.IsNullOrWhiteSpace(shell) ? _options.DefaultShell : shell.Trim();
        var fileName = Path.GetFileName(candidate);

        if (!_options.AllowedShells.Any(
                allowed => string.Equals(allowed, fileName, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(allowed, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ForbiddenException("Shell is not allowed.");
        }

        var resolved = TryResolveExecutable(candidate) ?? TryResolveExecutable(fileName);
        if (resolved is null &&
            string.Equals(fileName, "pwsh.exe", StringComparison.OrdinalIgnoreCase) &&
            _options.AllowedShells.Any(allowed =>
                string.Equals(allowed, "powershell.exe", StringComparison.OrdinalIgnoreCase)))
        {
            resolved = TryResolveExecutable("powershell.exe");
        }

        if (resolved is null)
        {
            throw new NotFoundException($"Shell executable '{fileName}' was not found on this machine.");
        }

        return resolved;
    }

    private static string? TryResolveExecutable(string candidate)
    {
        if (Path.IsPathRooted(candidate))
        {
            return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
        }

        var fileName = Path.GetFileName(candidate);
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(directory.Trim(), fileName);
            if (File.Exists(fullPath))
            {
                return Path.GetFullPath(fullPath);
            }
        }

        foreach (var wellKnown in GetWellKnownShellPaths(fileName))
        {
            if (File.Exists(wellKnown))
            {
                return Path.GetFullPath(wellKnown);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetWellKnownShellPaths(string fileName)
    {
        if (string.Equals(fileName, "pwsh.exe", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell",
                "7",
                "pwsh.exe");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "PowerShell",
                "7",
                "pwsh.exe");
        }

        if (string.Equals(fileName, "powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
        }
    }

    private static void KillProcessTree(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private static TerminalSessionDescriptor ToDescriptor(TerminalSession session) =>
        new(session.Id, session.PromptId, session.Shell, session.Cwd, session.CreatedAtUtc);

    private sealed record TerminalOutputChunk(Guid SessionId, byte[] Data);
}
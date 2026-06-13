using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;
using Thoth.Infrastructure.Terminals;

namespace Thoth.Infrastructure.UnitTests;

public sealed class TerminalSessionManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"prompttasks-terminal-{Guid.NewGuid():N}");
    private readonly FakePtyConnectionFactory _ptyFactory = new();
    private readonly RecordingTerminalNotifier _notifier = new();
    private readonly TerminalSessionManager _manager;

    public TerminalSessionManagerTests()
    {
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddScoped<ITerminalNotifier>(_ => _notifier);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _manager = new TerminalSessionManager(
            scopeFactory,
            _ptyFactory,
            Options.Create(new TerminalOptions
            {
                Enabled = true,
                MaxSessionsPerPrompt = 2,
                MaxTotalSessions = 4,
                OrphanTimeoutSeconds = 4,
                OutputFlushMilliseconds = 10,
                MaxOutputChunkBytes = 1024
            }),
            NullLogger<TerminalSessionManager>.Instance);
    }

    [Fact]
    public async Task CreateAsync_registers_session_with_descriptor()
    {
        var promptId = Guid.CreateVersion7();
        var descriptor = await _manager.CreateAsync(promptId, _root, string.Empty, null, CancellationToken.None);

        descriptor.PromptId.Should().Be(promptId);
        descriptor.Cwd.Should().Be(Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        descriptor.Shell.ToLowerInvariant().Should().EndWith("powershell.exe");
        _manager.ListForPrompt(promptId).Should().ContainSingle(item => item.Id == descriptor.Id);
    }

    [Fact]
    public async Task CreateAsync_delivers_initial_input_to_pty()
    {
        var promptId = Guid.CreateVersion7();
        var initialInput = "codex --yolo\r"u8.ToArray();

        await _manager.CreateAsync(promptId, _root, string.Empty, initialInput, CancellationToken.None);
        await Task.Delay(700);

        _ptyFactory.LastWritten.Should().BeEquivalentTo(initialInput);
    }

    [Fact]
    public async Task WriteInput_routes_bytes_to_pty()
    {
        var promptId = Guid.CreateVersion7();
        var descriptor = await _manager.CreateAsync(promptId, _root, string.Empty, null, CancellationToken.None);
        var input = "echo hi"u8.ToArray();

        _manager.WriteInput(descriptor.Id, input);

        _ptyFactory.LastWritten.Should().BeEquivalentTo(input);
    }

    [Fact]
    public async Task CloseAsync_notifies_exit_to_clients()
    {
        await _manager.StartAsync(CancellationToken.None);

        try
        {
            var promptId = Guid.CreateVersion7();
            var descriptor = await _manager.CreateAsync(promptId, _root, string.Empty, null, CancellationToken.None);
            await _manager.CloseAsync(descriptor.Id, CancellationToken.None);
            await Task.Delay(200);

            _notifier.Exits.Should().ContainSingle(item =>
                item.SessionId == descriptor.Id && item.ExitCode == -1);
        }
        finally
        {
            await _manager.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task KillForPromptAsync_removes_all_prompt_sessions()
    {
        var promptId = Guid.CreateVersion7();
        var first = await _manager.CreateAsync(promptId, _root, string.Empty, null, CancellationToken.None);
        var second = await _manager.CreateAsync(promptId, _root, string.Empty, null, CancellationToken.None);

        await _manager.KillForPromptAsync(promptId, CancellationToken.None);

        _manager.TryGetSession(first.Id).Should().BeNull();
        _manager.TryGetSession(second.Id).Should().BeNull();
        _manager.ListForPrompt(promptId).Should().BeEmpty();
        _ptyFactory.KilledCount.Should().Be(2);
    }

    [Fact]
    public async Task ReleaseConnection_detaches_without_killing_session()
    {
        var promptId = Guid.CreateVersion7();
        var descriptor = await _manager.CreateAsync(promptId, _root, string.Empty, null, CancellationToken.None);
        _manager.AttachConnection(descriptor.Id, "conn-1");
        _manager.ReleaseConnection("conn-1");

        _manager.TryGetSession(descriptor.Id).Should().NotBeNull();
        _ptyFactory.KilledCount.Should().Be(0);
    }

    public void Dispose()
    {
        _manager.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakePtyConnectionFactory : IPtyConnectionFactory
    {
        public byte[] LastWritten { get; set; } = [];
        public int KilledCount { get; set; }

        public Task<IPtyConnection> CreateAsync(
            string shell,
            string cwd,
            int cols,
            int rows,
            CancellationToken cancellationToken)
        {
            var connection = new FakePtyConnection(this);
            return Task.FromResult<IPtyConnection>(connection);
        }
    }

    private sealed class FakePtyConnection(FakePtyConnectionFactory factory) : IPtyConnection
    {
        private readonly CapturingWriteStream _writer = new(factory);
        private bool _killed;

        public int ProcessId => 4242;

        public Stream ReaderStream { get; } = new MemoryStream();

        public Stream WriterStream => _writer;

        public event EventHandler<int>? Exited;

        public void Resize(int cols, int rows)
        {
        }

        public void Kill()
        {
            if (_killed)
            {
                return;
            }

            _killed = true;
            factory.KilledCount++;
            Exited?.Invoke(this, 0);
        }

        public ValueTask DisposeAsync()
        {
            _writer.Dispose();
            ReaderStream.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingWriteStream(FakePtyConnectionFactory factory) : Stream
    {
        private readonly MemoryStream _inner = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            factory.LastWritten = _inner.ToArray();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class RecordingTerminalNotifier : ITerminalNotifier
    {
        public ConcurrentQueue<(Guid SessionId, string DataBase64)> Outputs { get; } = new();
        public ConcurrentQueue<(Guid SessionId, int ExitCode)> Exits { get; } = new();

        public Task TerminalOutputAsync(Guid sessionId, string dataBase64, CancellationToken cancellationToken)
        {
            Outputs.Enqueue((sessionId, dataBase64));
            return Task.CompletedTask;
        }

        public Task TerminalExitedAsync(Guid sessionId, int exitCode, CancellationToken cancellationToken)
        {
            Exits.Enqueue((sessionId, exitCode));
            return Task.CompletedTask;
        }
    }
}
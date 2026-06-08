using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Infrastructure.FileSystem;

namespace PromptTasks.Infrastructure.UnitTests;

public sealed class WorkspaceFileWatchCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"prompttasks-watch-{Guid.NewGuid():N}");
    private readonly Guid _workingDirectoryId = Guid.CreateVersion7();
    private readonly FakeApplicationDbContext _context = new();
    private readonly RecordingWorkspaceFileNotifier _notifier = new();
    private readonly WorkspaceFileWatchService _service;

    public WorkspaceFileWatchCoordinatorTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "tracked.txt"), "initial");

        _context.WorkingDirectoryItems.Add(new WorkingDirectory
        {
            Id = _workingDirectoryId,
            Name = "repo",
            AbsolutePath = _root,
            OwnerId = Guid.CreateVersion7(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => _context);
        services.AddScoped<IWorkspaceFileNotifier>(_ => _notifier);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _service = new WorkspaceFileWatchService(
            scopeFactory,
            Options.Create(new WorkspaceFileWatchOptions { DebounceMilliseconds = 100 }),
            NullLogger<WorkspaceFileWatchService>.Instance);
    }

    [Fact]
    public async Task JoinFile_notifies_when_tracked_file_changes()
    {
        await _service.StartAsync(CancellationToken.None);

        try
        {
            await _service.JoinFileAsync(_workingDirectoryId, "tracked.txt", "conn-1", CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(_root, "tracked.txt"), "updated");

            var notification = await _notifier.WaitForNextAsync(TimeSpan.FromSeconds(5));

            notification.WorkingDirectoryId.Should().Be(_workingDirectoryId);
            notification.RelativePath.Should().Be("tracked.txt");
        }
        finally
        {
            await _service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ReleaseConnection_stops_notifications_for_disconnected_client()
    {
        await _service.StartAsync(CancellationToken.None);

        try
        {
            await _service.JoinFileAsync(_workingDirectoryId, "tracked.txt", "conn-1", CancellationToken.None);
            _service.ReleaseConnection("conn-1");

            await File.WriteAllTextAsync(Path.Combine(_root, "tracked.txt"), "after disconnect");
            await Task.Delay(300);

            _notifier.Notifications.Should().BeEmpty();
        }
        finally
        {
            await _service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task LeaveFile_stops_notifications_when_last_client_leaves()
    {
        await _service.StartAsync(CancellationToken.None);

        try
        {
            await _service.JoinFileAsync(_workingDirectoryId, "tracked.txt", "conn-1", CancellationToken.None);
            await _service.LeaveFileAsync(_workingDirectoryId, "tracked.txt", "conn-1", CancellationToken.None);

            await File.WriteAllTextAsync(Path.Combine(_root, "tracked.txt"), "after leave");
            await Task.Delay(300);

            _notifier.Notifications.Should().BeEmpty();
        }
        finally
        {
            await _service.StopAsync(CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();

        public IQueryable<Domain.Users.User> Users => Array.Empty<Domain.Users.User>().AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<Domain.FutureTasks.FutureTask> FutureTasks => Array.Empty<Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Array.Empty<Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Domain.Prompts.Prompt> Prompts => Array.Empty<Domain.Prompts.Prompt>().AsQueryable();
        public IQueryable<Domain.Prompts.PromptVersion> PromptVersions => Array.Empty<Domain.Prompts.PromptVersion>().AsQueryable();
        public IQueryable<Domain.Prompts.PromptFileReference> PromptFileReferences => Array.Empty<Domain.Prompts.PromptFileReference>().AsQueryable();
        public IQueryable<Domain.Prompts.LinkedDocument> LinkedDocuments => Array.Empty<Domain.Prompts.LinkedDocument>().AsQueryable();
        public IQueryable<Domain.Prompts.LinkedDocumentVersion> LinkedDocumentVersions => Array.Empty<Domain.Prompts.LinkedDocumentVersion>().AsQueryable();
        public IQueryable<Domain.Workflows.WorkflowTemplate> WorkflowTemplates => Array.Empty<Domain.Workflows.WorkflowTemplate>().AsQueryable();
        public IQueryable<Domain.Workflows.WorkflowTemplatePhase> WorkflowTemplatePhases => Array.Empty<Domain.Workflows.WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<Domain.Workflows.PromptWorkflow> PromptWorkflows => Array.Empty<Domain.Workflows.PromptWorkflow>().AsQueryable();
        public IQueryable<Domain.Workflows.PromptWorkflowPhase> PromptWorkflowPhases => Array.Empty<Domain.Workflows.PromptWorkflowPhase>().AsQueryable();
        public IQueryable<Domain.Workflows.PromptWorkflowEvent> PromptWorkflowEvents => Array.Empty<Domain.Workflows.PromptWorkflowEvent>().AsQueryable();
        public IQueryable<Domain.Ai.AiChatSession> AiChatSessions => Array.Empty<Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<Domain.Ai.AiChatMessage> AiChatMessages => Array.Empty<Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<Domain.Ai.AiUserSettings> AiUserSettings => Array.Empty<Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<Domain.Notebooks.Notebook> Notebooks => Array.Empty<Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<Domain.Notebooks.Note> Notes => Array.Empty<Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<Domain.Diagrams.Diagram> Diagrams => Array.Empty<Domain.Diagrams.Diagram>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            if (entity is WorkingDirectory directory)
            {
                WorkingDirectoryItems.Add(directory);
            }
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        public void Remove<TEntity>(TEntity entity) where TEntity : class
        {
            if (entity is WorkingDirectory directory)
            {
                WorkingDirectoryItems.Remove(directory);
            }
        }

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities.ToList())
            {
                Remove(entity);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class RecordingWorkspaceFileNotifier : IWorkspaceFileNotifier
    {
        private readonly TaskCompletionSource<(Guid WorkingDirectoryId, string RelativePath)> _next =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(Guid WorkingDirectoryId, string RelativePath)> Notifications { get; } = new();

        public Task WorkspaceFileChangedAsync(
            Guid workingDirectoryId,
            string relativePath,
            CancellationToken cancellationToken)
        {
            var notification = (workingDirectoryId, relativePath);
            Notifications.Add(notification);
            _next.TrySetResult(notification);
            return Task.CompletedTask;
        }

        public async Task<(Guid WorkingDirectoryId, string RelativePath)> WaitForNextAsync(TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            return await _next.Task.WaitAsync(cts.Token);
        }
    }
}
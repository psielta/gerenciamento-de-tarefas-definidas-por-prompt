using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.LinkedDocuments.Commands.LinkDocument;
using PromptTasks.Application.Features.LinkedDocuments.Commands.ResumeLinkedDocument;
using PromptTasks.Application.Features.LinkedDocuments.Commands.SetLinkedDocumentPullRequest;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Application.UnitTests;

public sealed class LinkedDocumentHandlerTests
{
    [Fact]
    public async Task LinkDocument_creates_document_version_starts_watcher_and_notifies()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context);
        var fileService = new FakeLinkedDocumentFileService();
        var watcher = new FakeWatchCoordinator();
        var notifier = new FakeLinkedDocumentNotifier();
        var handler = new LinkDocumentHandler(
            context,
            fileService,
            watcher,
            notifier,
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var result = await handler.Handle(
            new LinkDocumentCommand(prompt.Id, "C:/plans/plan.md"),
            CancellationToken.None);

        result.PromptId.Should().Be(prompt.Id);
        result.Status.Should().Be(LinkedDocumentStatus.Tracking);
        result.CurrentVersion.Should().Be(1);
        context.LinkedDocumentItems.Should().ContainSingle(document => document.AbsolutePathKey == "c:/plans/plan.md");
        context.LinkedDocumentVersionItems.Should().ContainSingle(version =>
            version.LinkedDocumentId == result.Id &&
            version.VersionNumber == 1 &&
            version.Content == "# Plan");
        watcher.Started.Should().Contain(result.Id);
        notifier.Linked.Should().Be(result.Id);
    }

    [Fact]
    public async Task LinkDocument_rejects_duplicate_normalized_path()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context);
        context.LinkedDocumentItems.Add(new LinkedDocument
        {
            PromptId = prompt.Id,
            AbsolutePath = "C:/plans/plan.md",
            AbsolutePathKey = "c:/plans/plan.md"
        });

        var handler = new LinkDocumentHandler(
            context,
            new FakeLinkedDocumentFileService(),
            new FakeWatchCoordinator(),
            new FakeLinkedDocumentNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var act = () => handler.Handle(new LinkDocumentCommand(prompt.Id, "C:/plans/plan.md"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task LinkDocument_rejects_archived_prompt()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, PromptStatus.Archived);
        var watcher = new FakeWatchCoordinator();
        var handler = new LinkDocumentHandler(
            context,
            new FakeLinkedDocumentFileService(),
            watcher,
            new FakeLinkedDocumentNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var act = () => handler.Handle(new LinkDocumentCommand(prompt.Id, "C:/plans/plan.md"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        context.LinkedDocumentItems.Should().BeEmpty();
        watcher.Started.Should().BeEmpty();
    }

    [Fact]
    public async Task ResumeDocument_rejects_archived_prompt()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, PromptStatus.Archived);
        var document = new LinkedDocument
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            WorkingDirectoryId = prompt.WorkingDirectoryId,
            AbsolutePath = "C:/plans/plan.md",
            AbsolutePathKey = "c:/plans/plan.md",
            Status = LinkedDocumentStatus.Paused
        };
        context.LinkedDocumentItems.Add(document);
        var watcher = new FakeWatchCoordinator();
        var handler = new ResumeLinkedDocumentHandler(
            context,
            new FakeLinkedDocumentSyncService(),
            watcher,
            new FakeLinkedDocumentNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var act = () => handler.Handle(new ResumeLinkedDocumentCommand(document.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        document.Status.Should().Be(LinkedDocumentStatus.Paused);
        watcher.Started.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkDocument_rejects_second_plan_for_prompt()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context);
        context.LinkedDocumentItems.Add(new LinkedDocument
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            AbsolutePath = "C:/plans/existing.md",
            AbsolutePathKey = "c:/plans/existing.md"
        });
        var handler = new LinkDocumentHandler(
            context,
            new FakeLinkedDocumentFileService(),
            new FakeWatchCoordinator(),
            new FakeLinkedDocumentNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        // Caminho diferente, mas o prompt ja tem 1 plano -> regra de no maximo 1.
        var act = () => handler.Handle(new LinkDocumentCommand(prompt.Id, "C:/plans/another.md"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        context.LinkedDocumentItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetLinkedDocumentPullRequest_persists_trimmed_value_and_notifies()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context);
        var document = new LinkedDocument
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            WorkingDirectoryId = prompt.WorkingDirectoryId,
            AbsolutePath = "C:/plans/plan.md",
            AbsolutePathKey = "c:/plans/plan.md",
            Status = LinkedDocumentStatus.Tracking
        };
        context.LinkedDocumentItems.Add(document);
        var notifier = new FakeLinkedDocumentNotifier();
        var handler = new SetLinkedDocumentPullRequestHandler(
            context,
            notifier,
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var result = await handler.Handle(
            new SetLinkedDocumentPullRequestCommand(document.Id, "  #123  "),
            CancellationToken.None);

        result.PullRequestReference.Should().Be("#123");
        document.PullRequestReference.Should().Be("#123");
        notifier.Updated.Should().NotBeNull();
        notifier.Updated!.PullRequestReference.Should().Be("#123");
    }

    [Fact]
    public async Task SetLinkedDocumentPullRequest_clears_value_when_blank()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context);
        var document = new LinkedDocument
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            WorkingDirectoryId = prompt.WorkingDirectoryId,
            AbsolutePath = "C:/plans/plan.md",
            AbsolutePathKey = "c:/plans/plan.md",
            Status = LinkedDocumentStatus.Tracking,
            PullRequestReference = "#9"
        };
        context.LinkedDocumentItems.Add(document);
        var handler = new SetLinkedDocumentPullRequestHandler(
            context,
            new FakeLinkedDocumentNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var result = await handler.Handle(
            new SetLinkedDocumentPullRequestCommand(document.Id, "   "),
            CancellationToken.None);

        result.PullRequestReference.Should().BeNull();
        document.PullRequestReference.Should().BeNull();
    }

    private static Prompt SeedPrompt(FakeApplicationDbContext context, PromptStatus status = PromptStatus.Draft)
    {
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        var prompt = new Prompt
        {
            Id = Guid.CreateVersion7(),
            WorkingDirectoryId = directory.Id,
            Title = "Prompt",
            Content = "Content",
            Status = status,
            OwnerId = User.SystemUserId
        };

        context.WorkingDirectoryItems.Add(directory);
        context.PromptItems.Add(prompt);
        return prompt;
    }

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public List<User> UserItems { get; } = new();
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
        public List<Prompt> PromptItems { get; } = new();
        public List<PromptVersion> PromptVersionItems { get; } = new();
        public List<PromptFileReference> PromptFileReferenceItems { get; } = new();
        public List<LinkedDocument> LinkedDocumentItems { get; } = new();
        public List<LinkedDocumentVersion> LinkedDocumentVersionItems { get; } = new();

        public IQueryable<User> Users => UserItems.AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.FutureTasks.FutureTask> FutureTasks => Enumerable.Empty<PromptTasks.Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<PromptTasks.Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<PromptTasks.Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Prompt> Prompts => PromptItems.AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => PromptVersionItems.AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => PromptFileReferenceItems.AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => LinkedDocumentItems.AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => LinkedDocumentVersionItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.WorkflowTemplate> WorkflowTemplates => Enumerable.Empty<PromptTasks.Domain.Workflows.WorkflowTemplate>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.WorkflowTemplatePhase> WorkflowTemplatePhases => Enumerable.Empty<PromptTasks.Domain.Workflows.WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.PromptWorkflow> PromptWorkflows => Enumerable.Empty<PromptTasks.Domain.Workflows.PromptWorkflow>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.PromptWorkflowPhase> PromptWorkflowPhases => Enumerable.Empty<PromptTasks.Domain.Workflows.PromptWorkflowPhase>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.PromptWorkflowEvent> PromptWorkflowEvents => Enumerable.Empty<PromptTasks.Domain.Workflows.PromptWorkflowEvent>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<PromptTasks.Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<PromptTasks.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Note> Notes => Enumerable.Empty<PromptTasks.Domain.Notebooks.Note>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case LinkedDocument document:
                    LinkedDocumentItems.Add(document);
                    break;
                case LinkedDocumentVersion version:
                    LinkedDocumentVersionItems.Add(version);
                    break;
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
            if (entity is LinkedDocument document)
            {
                LinkedDocumentItems.Remove(document);
            }
        }

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities.ToList())
            {
                Remove(entity);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeLinkedDocumentFileService : ILinkedDocumentFileService
    {
        public Task<MarkdownFileValidation> ValidateAsync(string absolutePath, CancellationToken cancellationToken) =>
            Task.FromResult(MarkdownFileValidation.Valid(absolutePath, absolutePath.ToLowerInvariant(), 6));

        public Task<MarkdownFileReadResult> ReadAsync(string absolutePath, CancellationToken cancellationToken) =>
            Task.FromResult(MarkdownFileReadResult.Valid("# Plan", "hash", 6));
    }

    private sealed class FakeLinkedDocumentSyncService : ILinkedDocumentSyncService
    {
        public Task<LinkedDocumentSyncOutcome> SyncAsync(
            Guid linkedDocumentId,
            LinkedDocumentVersionSource source,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LinkedDocumentSyncOutcome(null, null, false, false));
    }

    private sealed class FakeWatchCoordinator : ILinkedDocumentWatchCoordinator
    {
        public List<Guid> Started { get; } = new();

        public Task StartTrackingAsync(Guid linkedDocumentId, CancellationToken cancellationToken)
        {
            Started.Add(linkedDocumentId);
            return Task.CompletedTask;
        }

        public void StopTracking(Guid linkedDocumentId)
        {
        }
    }

    private sealed class FakeLinkedDocumentNotifier : ILinkedDocumentNotifier
    {
        public Guid? Linked { get; private set; }

        public Task LinkedDocumentLinkedAsync(
            LinkedDocumentDto document,
            Guid workingDirectoryId,
            CancellationToken cancellationToken)
        {
            Linked = document.Id;
            return Task.CompletedTask;
        }

        public LinkedDocumentDto? Updated { get; private set; }

        public Task LinkedDocumentUpdatedAsync(
            LinkedDocumentDto document,
            Guid workingDirectoryId,
            CancellationToken cancellationToken)
        {
            Updated = document;
            return Task.CompletedTask;
        }

        public Task LinkedDocumentRemovedAsync(
            Guid linkedDocumentId,
            Guid promptId,
            Guid workingDirectoryId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
    }
}

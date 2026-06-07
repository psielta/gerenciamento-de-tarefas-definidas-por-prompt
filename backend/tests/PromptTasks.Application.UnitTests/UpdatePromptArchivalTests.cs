using FluentAssertions;
using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Prompts.Commands.UpdatePrompt;
using PromptTasks.Application.Features.Prompts.Commands.UpdatePromptStatus;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Application.UnitTests;

public sealed class UpdatePromptArchivalTests
{
    [Fact]
    public async Task UpdatePromptStatus_archiving_prompt_pauses_active_linked_documents()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, PromptStatus.Ready);
        var tracking = SeedLinkedDocument(context, prompt, LinkedDocumentStatus.Tracking);
        var error = SeedLinkedDocument(context, prompt, LinkedDocumentStatus.Error);
        var missing = SeedLinkedDocument(context, prompt, LinkedDocumentStatus.Missing);
        var alreadyPaused = SeedLinkedDocument(context, prompt, LinkedDocumentStatus.Paused);
        var otherPrompt = SeedPrompt(context, PromptStatus.Ready);
        var otherTracking = SeedLinkedDocument(context, otherPrompt, LinkedDocumentStatus.Tracking);
        var promptNotifier = new FakePromptNotifier();
        var watcher = new FakeWatchCoordinator();
        var linkedDocumentNotifier = new FakeLinkedDocumentNotifier();
        var clock = new FakeDateTimeProvider();
        var handler = new UpdatePromptStatusHandler(
            context,
            promptNotifier,
            watcher,
            linkedDocumentNotifier,
            new FakeCurrentUser(),
            clock,
            new NoOpSender());

        var result = await handler.Handle(
            new UpdatePromptStatusCommand(prompt.Id, PromptStatus.Archived, "0"),
            CancellationToken.None);

        result.Status.Should().Be(PromptStatus.Archived);
        prompt.Status.Should().Be(PromptStatus.Archived);
        prompt.CurrentVersion.Should().Be(2);
        context.PromptVersionItems.Should().ContainSingle(version =>
            version.PromptId == prompt.Id &&
            version.VersionNumber == 2 &&
            version.Status == PromptStatus.Archived &&
            version.ChangeNote == "Status changed");
        promptNotifier.Updated.Should().Be(result);

        tracking.Status.Should().Be(LinkedDocumentStatus.Paused);
        error.Status.Should().Be(LinkedDocumentStatus.Paused);
        missing.Status.Should().Be(LinkedDocumentStatus.Paused);
        tracking.UpdatedAtUtc.Should().Be(clock.UtcNow);
        error.UpdatedAtUtc.Should().Be(clock.UtcNow);
        missing.UpdatedAtUtc.Should().Be(clock.UtcNow);
        alreadyPaused.Status.Should().Be(LinkedDocumentStatus.Paused);
        alreadyPaused.UpdatedAtUtc.Should().NotBe(clock.UtcNow);
        otherTracking.Status.Should().Be(LinkedDocumentStatus.Tracking);

        watcher.Stopped.Should().BeEquivalentTo(new[] { tracking.Id, error.Id, missing.Id });
        linkedDocumentNotifier.Updated.Select(item => item.Document.Id).Should()
            .BeEquivalentTo(new[] { tracking.Id, error.Id, missing.Id });
        linkedDocumentNotifier.Updated.Should().OnlyContain(item =>
            item.WorkingDirectoryId == prompt.WorkingDirectoryId &&
            item.Document.Status == LinkedDocumentStatus.Paused);
    }

    [Fact]
    public async Task UpdatePrompt_archiving_prompt_pauses_active_linked_documents()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, PromptStatus.Ready);
        var tracking = SeedLinkedDocument(context, prompt, LinkedDocumentStatus.Tracking);
        var promptNotifier = new FakePromptNotifier();
        var watcher = new FakeWatchCoordinator();
        var linkedDocumentNotifier = new FakeLinkedDocumentNotifier();
        var handler = new UpdatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            promptNotifier,
            watcher,
            linkedDocumentNotifier,
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var result = await handler.Handle(
            new UpdatePromptCommand(
                prompt.Id,
                "Archived prompt",
                "Archived content",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Archived,
                "0",
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.Status.Should().Be(PromptStatus.Archived);
        result.Title.Should().Be("Archived prompt");
        tracking.Status.Should().Be(LinkedDocumentStatus.Paused);
        watcher.Stopped.Should().ContainSingle().Which.Should().Be(tracking.Id);
        linkedDocumentNotifier.Updated.Should().ContainSingle(item =>
            item.Document.Id == tracking.Id &&
            item.Document.Status == LinkedDocumentStatus.Paused);
        promptNotifier.Updated.Should().Be(result);
        context.PromptVersionItems.Should().ContainSingle(version =>
            version.PromptId == prompt.Id &&
            version.VersionNumber == 2 &&
            version.Status == PromptStatus.Archived &&
            version.ChangeNote == "Updated");
    }

    private static Prompt SeedPrompt(FakeApplicationDbContext context, PromptStatus status)
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
            TargetAgent = TargetAgent.ClaudeCode,
            Kind = PromptKind.General,
            Status = status,
            CurrentVersion = 1,
            OwnerId = User.SystemUserId,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero)
        };

        context.WorkingDirectoryItems.Add(directory);
        context.PromptItems.Add(prompt);
        return prompt;
    }

    private static LinkedDocument SeedLinkedDocument(
        FakeApplicationDbContext context,
        Prompt prompt,
        LinkedDocumentStatus status)
    {
        var document = new LinkedDocument
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            WorkingDirectoryId = prompt.WorkingDirectoryId,
            AbsolutePath = $"C:/plans/{Guid.CreateVersion7()}.md",
            AbsolutePathKey = Guid.CreateVersion7().ToString(),
            DocumentType = LinkedDocumentType.ClaudeCodePlan,
            DisplayName = "plan.md",
            Status = status,
            CurrentVersion = 1,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 5, 29, 12, 0, 0, TimeSpan.Zero)
        };

        context.LinkedDocumentItems.Add(document);
        return document;
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
                case Prompt prompt:
                    PromptItems.Add(prompt);
                    break;
                case PromptVersion version:
                    PromptVersionItems.Add(version);
                    break;
                case PromptFileReference reference:
                    PromptFileReferenceItems.Add(reference);
                    break;
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
            switch (entity)
            {
                case Prompt prompt:
                    PromptItems.Remove(prompt);
                    break;
                case PromptFileReference reference:
                    PromptFileReferenceItems.Remove(reference);
                    break;
                case LinkedDocument document:
                    LinkedDocumentItems.Remove(document);
                    break;
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

    private sealed class FakePromptNotifier : IPromptNotifier
    {
        public PromptDto? Updated { get; private set; }

        public Task PromptCreatedAsync(PromptDto prompt, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PromptUpdatedAsync(PromptDto prompt, CancellationToken cancellationToken)
        {
            Updated = prompt;
            return Task.CompletedTask;
        }

        public Task PromptDeletedAsync(Guid promptId, Guid workingDirectoryId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeLinkedDocumentNotifier : ILinkedDocumentNotifier
    {
        public List<(LinkedDocumentDto Document, Guid WorkingDirectoryId)> Updated { get; } = new();

        public Task LinkedDocumentLinkedAsync(
            LinkedDocumentDto document,
            Guid workingDirectoryId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task LinkedDocumentUpdatedAsync(
            LinkedDocumentDto document,
            Guid workingDirectoryId,
            CancellationToken cancellationToken)
        {
            Updated.Add((document, workingDirectoryId));
            return Task.CompletedTask;
        }

        public Task LinkedDocumentRemovedAsync(
            Guid linkedDocumentId,
            Guid promptId,
            Guid workingDirectoryId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeWatchCoordinator : ILinkedDocumentWatchCoordinator
    {
        public List<Guid> Stopped { get; } = new();

        public Task StartTrackingAsync(Guid linkedDocumentId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void StopTracking(Guid linkedDocumentId)
        {
            Stopped.Add(linkedDocumentId);
        }
    }

    private sealed class FakeWorkspaceFileService : IWorkspaceFileService
    {
        public Task<ValidatedPathResult> ValidatePathAsync(string absolutePath, CancellationToken cancellationToken) =>
            Task.FromResult(ValidatedPathResult.Valid(absolutePath));

        public Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<string?> ReadSelectedFilesAsync(
            string rootAbsolutePath,
            IReadOnlyList<string> relativePaths,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<FileSearchResultDto>> SearchAsync(
            Guid workingDirectoryId,
            string rootAbsolutePath,
            string query,
            int limit,
            bool respectGitignore,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FileSearchResultDto>>(Array.Empty<FileSearchResultDto>());

        public Task<IReadOnlyList<FileReferenceValidationDto>> ValidateRelativePathsAsync(
            string rootAbsolutePath,
            IReadOnlyList<string> relativePaths,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FileReferenceValidationDto>>(Array.Empty<FileReferenceValidationDto>());

        public Task<FileReferenceResolution> ResolveRelativePathAsync(
            string rootAbsolutePath,
            string relativePath,
            CancellationToken cancellationToken) =>
            Task.FromResult(new FileReferenceResolution(relativePath, true, DateTimeOffset.UtcNow));

        public Task<IReadOnlyList<DirectoryEntryDto>> BrowseDirectoryAsync(
            string rootAbsolutePath,
            string relativeDirectoryPath,
            bool respectGitignore,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DirectoryEntryDto>>(Array.Empty<DirectoryEntryDto>());

        public Task<FileContentDto> ReadFileAsync(
            string rootAbsolutePath,
            string relativePath,
            CancellationToken cancellationToken) =>
            Task.FromResult(new FileContentDto(relativePath, string.Empty, 0, false, false));
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class NoOpSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult<TResponse>(default!);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<TResponse>();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            Task.FromResult<object?>(null);

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<object?>();
    }
}

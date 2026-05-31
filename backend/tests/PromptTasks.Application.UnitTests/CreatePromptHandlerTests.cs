using FluentAssertions;
using FluentValidation;
using PromptTasks.Application.Common.Behaviors;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Prompts.Commands.CreatePrompt;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.UnitTests;

public sealed class CreatePromptHandlerTests
{
    [Fact]
    public async Task Handle_creates_prompt_version_references_and_notifies()
    {
        var context = new FakeApplicationDbContext();
        context.WorkingDirectoryItems.Add(new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        });
        var notifier = new FakePromptNotifier();

        var handler = new CreatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            notifier,
            new FakeWorkflowNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var command = new CreatePromptCommand(
            context.WorkingDirectoryItems[0].Id,
            null,
            "Fix main",
            "Please inspect @src/main.go",
            TargetAgent.Codex,
            PromptKind.General,
            PromptStatus.Draft,
            new[] { new FileMentionDto("src/main.go", "src/main.go") });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Title.Should().Be("Fix main");
        context.PromptItems.Should().ContainSingle();
        context.PromptVersionItems.Should().ContainSingle(version => version.VersionNumber == 1);
        context.PromptFileReferenceItems.Should().ContainSingle(reference => reference.RelativePath == "src/main.go");
        notifier.Created.Should().Be(result);
        context.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidationBehavior_aggregates_validation_failures()
    {
        var behavior = new ValidationBehavior<CreatePromptCommand, PromptDto>(new[] { new CreatePromptValidator() });
        var invalid = new CreatePromptCommand(Guid.Empty, null, "", "", (TargetAgent)999, PromptKind.General, PromptStatus.Draft, null);

        var act = () => behavior.Handle(
            invalid,
            _ => Task.FromResult(new PromptDto(
                Guid.Empty,
                Guid.Empty,
                null,
                "",
                "",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                1,
                "0",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                Array.Empty<FileMentionDto>())),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_links_child_prompt_to_parent_in_same_working_directory()
    {
        var context = new FakeApplicationDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        var parent = new Prompt
        {
            WorkingDirectoryId = directory.Id,
            Title = "Parent",
            Content = "Parent content",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        context.PromptItems.Add(parent);
        var handler = new CreatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            new FakePromptNotifier(),
            new FakeWorkflowNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var result = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                parent.Id,
                "Review child",
                "Review the plan",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Draft,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.ParentPromptId.Should().Be(parent.Id);
        context.PromptItems.Should().ContainSingle(prompt => prompt.ParentPromptId == parent.Id);
    }

    [Fact]
    public async Task Handle_rejects_child_prompt_from_another_working_directory()
    {
        var context = new FakeApplicationDbContext();
        var parentDirectory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "parent",
            AbsolutePath = "C:/repo-parent",
            OwnerId = User.SystemUserId
        };
        var childDirectory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "child",
            AbsolutePath = "C:/repo-child",
            OwnerId = User.SystemUserId
        };
        var parent = new Prompt
        {
            WorkingDirectoryId = parentDirectory.Id,
            Title = "Parent",
            Content = "Parent content",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(parentDirectory);
        context.WorkingDirectoryItems.Add(childDirectory);
        context.PromptItems.Add(parent);
        var handler = new CreatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            new FakePromptNotifier(),
            new FakeWorkflowNotifier(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider());

        var act = () => handler.Handle(
            new CreatePromptCommand(
                childDirectory.Id,
                parent.Id,
                "Review child",
                "Review the plan",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Draft,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
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
        public List<WorkflowTemplate> WorkflowTemplateItems { get; } = new();
        public List<WorkflowTemplatePhase> WorkflowTemplatePhaseItems { get; } = new();
        public List<PromptWorkflow> PromptWorkflowItems { get; } = new();
        public List<PromptWorkflowPhase> PromptWorkflowPhaseItems { get; } = new();
        public List<PromptWorkflowEvent> PromptWorkflowEventItems { get; } = new();
        public int SaveChangesCount { get; private set; }

        public IQueryable<User> Users => UserItems.AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<Prompt> Prompts => PromptItems.AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => PromptVersionItems.AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => PromptFileReferenceItems.AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => LinkedDocumentItems.AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => LinkedDocumentVersionItems.AsQueryable();
        public IQueryable<WorkflowTemplate> WorkflowTemplates => WorkflowTemplateItems.AsQueryable();
        public IQueryable<WorkflowTemplatePhase> WorkflowTemplatePhases => WorkflowTemplatePhaseItems.AsQueryable();
        public IQueryable<PromptWorkflow> PromptWorkflows => PromptWorkflowItems.AsQueryable();
        public IQueryable<PromptWorkflowPhase> PromptWorkflowPhases => PromptWorkflowPhaseItems.AsQueryable();
        public IQueryable<PromptWorkflowEvent> PromptWorkflowEvents => PromptWorkflowEventItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<PromptTasks.Domain.Ai.AiUserSettings>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            AddToList(entity);
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities)
            {
                AddToList(entity);
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
            }
        }

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities.ToList())
            {
                Remove(entity);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        private void AddToList<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case Prompt prompt:
                    PromptItems.Add(prompt);
                    break;
                case PromptVersion version:
                    PromptVersionItems.Add(version);
                    break;
                case LinkedDocumentVersion version:
                    LinkedDocumentVersionItems.Add(version);
                    break;
                case WorkflowTemplate template:
                    WorkflowTemplateItems.Add(template);
                    break;
                case WorkflowTemplatePhase templatePhase:
                    WorkflowTemplatePhaseItems.Add(templatePhase);
                    break;
                case PromptWorkflow workflow:
                    PromptWorkflowItems.Add(workflow);
                    break;
                case PromptWorkflowPhase workflowPhase:
                    PromptWorkflowPhaseItems.Add(workflowPhase);
                    break;
                case PromptWorkflowEvent workflowEvent:
                    PromptWorkflowEventItems.Add(workflowEvent);
                    break;
                case PromptFileReference reference:
                    PromptFileReferenceItems.Add(reference);
                    break;
                case WorkingDirectory directory:
                    WorkingDirectoryItems.Add(directory);
                    break;
            }
        }
    }

    private sealed class FakeWorkspaceFileService : IWorkspaceFileService
    {
        public Task<ValidatedPathResult> ValidatePathAsync(string absolutePath, CancellationToken cancellationToken) =>
            Task.FromResult(ValidatedPathResult.Valid(absolutePath));

        public Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken) =>
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
            Task.FromResult<IReadOnlyList<FileReferenceValidationDto>>(
                relativePaths
                    .Select(path => new FileReferenceValidationDto(path, path, true, false, null))
                    .ToList());

        public Task<FileReferenceResolution> ResolveRelativePathAsync(
            string rootAbsolutePath,
            string relativePath,
            CancellationToken cancellationToken) =>
            Task.FromResult(new FileReferenceResolution(relativePath, true, DateTimeOffset.UtcNow));
    }

    private sealed class FakePromptNotifier : IPromptNotifier
    {
        public PromptDto? Created { get; private set; }

        public Task PromptCreatedAsync(PromptDto prompt, CancellationToken cancellationToken)
        {
            Created = prompt;
            return Task.CompletedTask;
        }

        public Task PromptUpdatedAsync(PromptDto prompt, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PromptDeletedAsync(Guid promptId, Guid workingDirectoryId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeWorkflowNotifier : IWorkflowNotifier
    {
        public Task TaskWorkflowChangedAsync(TaskSummaryDto summary, CancellationToken cancellationToken) =>
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

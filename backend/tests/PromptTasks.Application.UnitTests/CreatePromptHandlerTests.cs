using FluentAssertions;
using FluentValidation;
using PromptTasks.Application.Common.Behaviors;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.PromptTemplates;
using PromptTasks.Application.Features.PromptTemplates.Definitions;
using PromptTasks.Application.Features.Prompts.Commands.CreatePrompt;
using PromptTasks.Domain.FutureTasks;
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
            new FakeDailyTaskSequenceProvider(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

        var command = new CreatePromptCommand(
            context.WorkingDirectoryItems[0].Id,
            null,
            null,
            "Fix main",
            "Please inspect @src/main.go",
            TargetAgent.Codex,
            PromptKind.General,
            PromptStatus.Draft,
            null,
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
        var invalid = new CreatePromptCommand(Guid.Empty, null, null, "", "", (TargetAgent)999, PromptKind.General, PromptStatus.Draft, null, null);

        var act = () => behavior.Handle(
            invalid,
            _ => Task.FromResult(new PromptDto(
                Guid.Empty,
                Guid.Empty,
                null,
                null,
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
            new FakeDailyTaskSequenceProvider(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

        var result = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                parent.Id,
                null,
                "Review child",
                "Review the plan",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.ParentPromptId.Should().Be(parent.Id);
        result.TaskNumber.Should().BeNull();
        context.PromptItems.Should().ContainSingle(prompt => prompt.ParentPromptId == parent.Id);
    }

    [Fact]
    public async Task Handle_generates_task_number_for_root_prompt_when_pattern_is_configured()
    {
        var context = new FakeApplicationDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            TaskNumberPattern = "BP{N:000}{Date}",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        var sequenceProvider = new FakeDailyTaskSequenceProvider(12);
        var handler = new CreatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            new FakePromptNotifier(),
            new FakeWorkflowNotifier(),
            sequenceProvider,
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

        var result = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                null,
                null,
                "Root task",
                "Create a plan",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.TaskNumber.Should().Be("BP012300526");
        context.PromptItems.Should().ContainSingle(prompt => prompt.TaskNumber == "BP012300526");
        sequenceProvider.Requests.Should().ContainSingle(request =>
            request.WorkingDirectoryId == directory.Id && request.DateUtc == new DateOnly(2026, 5, 30));
    }

    [Fact]
    public async Task Handle_does_not_generate_task_number_without_pattern()
    {
        var context = new FakeApplicationDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        var sequenceProvider = new FakeDailyTaskSequenceProvider();
        var handler = new CreatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            new FakePromptNotifier(),
            new FakeWorkflowNotifier(),
            sequenceProvider,
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

        var result = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                null,
                null,
                "Root task",
                "Create a plan",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.TaskNumber.Should().BeNull();
        sequenceProvider.Requests.Should().BeEmpty();
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
            new FakeDailyTaskSequenceProvider(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

        var act = () => handler.Handle(
            new CreatePromptCommand(
                childDirectory.Id,
                parent.Id,
                null,
                "Review child",
                "Review the plan",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_advances_parent_workflow_from_source_template_key()
    {
        var context = new FakeApplicationDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        var notifier = new FakeWorkflowNotifier();
        var handler = new CreatePromptHandler(
            context,
            new FakeWorkspaceFileService(),
            new FakePromptNotifier(),
            notifier,
            new FakeDailyTaskSequenceProvider(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

        var parent = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                null,
                null,
                "Parent task",
                "Create a plan",
                TargetAgent.ClaudeCode,
                PromptKind.Planning,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);
        var parentWorkflow = context.PromptWorkflowItems.Single(workflow => workflow.PromptId == parent.Id);
        parentWorkflow.CurrentPhaseName.Should().Be("Engenharia de prompt");
        parentWorkflow.CurrentPhaseIteration.Should().Be(1);
        foreach (var phase in context.PromptWorkflowPhaseItems.Where(phase => phase.PromptWorkflowId == parentWorkflow.Id))
        {
            phase.Role = null;
        }

        await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                parent.Id,
                null,
                "Review plan",
                "Review",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Draft,
                PromptTemplateKey.ReviewPlanWithParentPrompt,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        parentWorkflow.CurrentPhaseName.Should().Be("Revisão do plano");
        parentWorkflow.CurrentActor.Should().Be(WorkflowActor.Codex);
        parentWorkflow.CurrentPhaseIteration.Should().Be(1);
        context.PromptWorkflowEventItems.Should().Contain(@event =>
            @event.PromptWorkflowId == parentWorkflow.Id &&
            @event.Type == WorkflowEventType.PhaseChanged &&
            @event.Note == "Gerado via \"Revisar plano com prompt pai\"");

        var reviewPhase = context.PromptWorkflowPhaseItems.Single(phase =>
            phase.PromptWorkflowId == parentWorkflow.Id &&
            phase.Name == parentWorkflow.CurrentPhaseName);
        reviewPhase.Role.Should().Be(WorkflowPhaseRole.PlanReview);
        context.PromptWorkflowEventItems.Add(new PromptWorkflowEvent
        {
            PromptWorkflowId = parentWorkflow.Id,
            Type = WorkflowEventType.PhaseChanged,
            PhaseId = reviewPhase.Id,
            PhaseNameSnapshot = reviewPhase.Name,
            Actor = reviewPhase.DefaultActor,
            Note = null,
            OccurredAtUtc = DateTimeOffset.UtcNow
        });

        await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                parent.Id,
                null,
                "Re-review plan",
                "Re-review",
                TargetAgent.Codex,
                PromptKind.Planning,
                PromptStatus.Draft,
                PromptTemplateKey.ReReviewPlan,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        parentWorkflow.CurrentPhaseName.Should().Be("Revisão do plano");
        parentWorkflow.CurrentPhaseIteration.Should().Be(2);
        context.PromptWorkflowEventItems.Should().Contain(@event =>
            @event.PromptWorkflowId == parentWorkflow.Id &&
            @event.Type == WorkflowEventType.PhaseChanged &&
            @event.Note == "Re-review #2 - Gerado via \"Re-review do plano\"");
        notifier.Changes.Should().Contain(summary =>
            summary.PromptId == parent.Id &&
            summary.CurrentPhaseName == "Revisão do plano" &&
            summary.CurrentPhaseIteration == 2);

        var unchangedPhaseName = parentWorkflow.CurrentPhaseName;
        var unchangedIteration = parentWorkflow.CurrentPhaseIteration;
        var unchangedEventCount = context.PromptWorkflowEventItems.Count;
        await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                parent.Id,
                null,
                "Manual child",
                "Manual",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        parentWorkflow.CurrentPhaseName.Should().Be(unchangedPhaseName);
        parentWorkflow.CurrentPhaseIteration.Should().Be(unchangedIteration);
        context.PromptWorkflowEventItems.Should().HaveCount(unchangedEventCount);

        await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                parent.Id,
                null,
                "Merge PR",
                "Merge",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                PromptTemplateKey.MergePullRequest,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        parentWorkflow.CurrentPhaseName.Should().Be("Commit/Merge");
        parentWorkflow.CurrentActor.Should().Be(WorkflowActor.Codex);
        parentWorkflow.CurrentPhaseIteration.Should().Be(1);
        context.PromptWorkflowEventItems.Should().Contain(@event =>
            @event.PromptWorkflowId == parentWorkflow.Id &&
            @event.Type == WorkflowEventType.PhaseChanged &&
            @event.Note == "Gerado via \"Fazer merge da PR\"");
    }

    [Fact]
    public async Task Handle_rejects_future_task_from_another_working_directory()
    {
        var context = new FakeApplicationDbContext();
        var promptDirectory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        var otherDirectory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "other",
            AbsolutePath = "C:/other",
            OwnerId = User.SystemUserId
        };
        var futureTask = new FutureTask
        {
            WorkingDirectoryId = otherDirectory.Id,
            Title = "Backlog item",
            Description = "Do something",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(promptDirectory);
        context.WorkingDirectoryItems.Add(otherDirectory);
        context.FutureTaskItems.Add(futureTask);
        var handler = CreateHandler(context);

        var act = () => handler.Handle(
            new CreatePromptCommand(
                promptDirectory.Id,
                null,
                futureTask.Id,
                "From backlog",
                "Work on it",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_links_future_task_and_advances_open_status_to_in_progress()
    {
        var context = new FakeApplicationDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        var futureTask = new FutureTask
        {
            WorkingDirectoryId = directory.Id,
            Title = "Backlog item",
            Description = "Do something",
            Status = FutureTaskStatus.Open,
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        context.FutureTaskItems.Add(futureTask);
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                null,
                futureTask.Id,
                "From backlog",
                "Work on it",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.FutureTaskId.Should().Be(futureTask.Id);
        context.PromptItems.Should().ContainSingle(prompt => prompt.FutureTaskId == futureTask.Id);
        futureTask.Status.Should().Be(FutureTaskStatus.InProgress);
    }

    [Theory]
    [InlineData(FutureTaskStatus.Done)]
    [InlineData(FutureTaskStatus.Archived)]
    public async Task Handle_keeps_future_task_status_when_not_open(FutureTaskStatus status)
    {
        var context = new FakeApplicationDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        var futureTask = new FutureTask
        {
            WorkingDirectoryId = directory.Id,
            Title = "Backlog item",
            Description = "Do something",
            Status = status,
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        context.FutureTaskItems.Add(futureTask);
        var handler = CreateHandler(context);

        var result = await handler.Handle(
            new CreatePromptCommand(
                directory.Id,
                null,
                futureTask.Id,
                "From backlog",
                "Work on it",
                TargetAgent.Codex,
                PromptKind.General,
                PromptStatus.Draft,
                null,
                Array.Empty<FileMentionDto>()),
            CancellationToken.None);

        result.FutureTaskId.Should().Be(futureTask.Id);
        futureTask.Status.Should().Be(status);
    }

    private static CreatePromptHandler CreateHandler(FakeApplicationDbContext context) =>
        new(
            context,
            new FakeWorkspaceFileService(),
            new FakePromptNotifier(),
            new FakeWorkflowNotifier(),
            new FakeDailyTaskSequenceProvider(),
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            CreateCatalog());

    private static PromptTemplateCatalog CreateCatalog() =>
        new(new IPromptTemplateDefinition[]
        {
            new ReviewPlanTemplate(),
            new ImplementPlanTemplate(),
            new ReviewPlanWithParentPromptTemplate(),
            new ReReviewPlanTemplate(),
            new ImplementPlanInWorktreeTemplate(),
            new ReviewPullRequestTemplate(),
            new ReReviewPullRequestTemplate(),
            new RebaseCurrentBranchTemplate(),
            new MergePullRequestTemplate()
        });

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public List<User> UserItems { get; } = new();
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
        public List<FutureTask> FutureTaskItems { get; } = new();
        public List<FutureTaskLabel> FutureTaskLabelItems { get; } = new();
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
        public IQueryable<FutureTask> FutureTasks => FutureTaskItems.AsQueryable();
        public IQueryable<FutureTaskLabel> FutureTaskLabels => FutureTaskLabelItems.AsQueryable();
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
        public IQueryable<PromptTasks.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<PromptTasks.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Note> Notes => Enumerable.Empty<PromptTasks.Domain.Notebooks.Note>().AsQueryable();

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
                case FutureTask futureTask:
                    FutureTaskItems.Remove(futureTask);
                    break;
                case FutureTaskLabel futureTaskLabel:
                    FutureTaskLabelItems.Remove(futureTaskLabel);
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
                case FutureTask futureTask:
                    FutureTaskItems.Add(futureTask);
                    break;
                case FutureTaskLabel futureTaskLabel:
                    FutureTaskLabelItems.Add(futureTaskLabel);
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
            Task.FromResult<IReadOnlyList<FileReferenceValidationDto>>(
                relativePaths
                    .Select(path => new FileReferenceValidationDto(path, path, true, false, null))
                    .ToList());

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
        public List<TaskSummaryDto> Changes { get; } = new();

        public Task TaskWorkflowChangedAsync(TaskSummaryDto summary, CancellationToken cancellationToken)
        {
            Changes.Add(summary);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class FakeDailyTaskSequenceProvider(int next = 1) : IDailyTaskSequenceProvider
    {
        public List<(Guid WorkingDirectoryId, DateOnly DateUtc)> Requests { get; } = new();

        public Task<int> NextAsync(Guid workingDirectoryId, DateOnly dateUtc, CancellationToken cancellationToken)
        {
            Requests.Add((workingDirectoryId, dateUtc));
            return Task.FromResult(next);
        }
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
    }
}

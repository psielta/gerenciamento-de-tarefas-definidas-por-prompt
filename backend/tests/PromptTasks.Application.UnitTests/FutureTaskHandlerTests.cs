using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Features.FutureTasks.Commands.CreateFutureTask;
using PromptTasks.Application.Features.FutureTasks.Commands.UpdateFutureTaskStatus;
using PromptTasks.Domain.Ai;
using PromptTasks.Domain.FutureTasks;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.UnitTests;

public sealed class FutureTaskHandlerTests
{
    [Fact]
    public async Task Create_persists_task_with_labels_and_returns_open_dto()
    {
        var context = new FakeDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        var handler = new CreateFutureTaskHandler(context, new FakeCurrentUser());

        var result = await handler.Handle(
            new CreateFutureTaskCommand(
                directory.Id,
                "Investigate flaky test",
                "It fails sometimes",
                FutureTaskType.Bug,
                new[] { "backend", "priority:high" },
                null),
            CancellationToken.None);

        result.Status.Should().Be(FutureTaskStatus.Open);
        result.Type.Should().Be(FutureTaskType.Bug);
        result.Labels.Should().BeEquivalentTo("backend", "priority:high");
        result.LinkedPromptCount.Should().Be(0);
        context.FutureTaskItems.Should().ContainSingle(task => task.Title == "Investigate flaky test");
        context.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Create_rejects_working_directory_owned_by_another_user()
    {
        var context = new FakeDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = Guid.CreateVersion7()
        };
        context.WorkingDirectoryItems.Add(directory);
        var handler = new CreateFutureTaskHandler(context, new FakeCurrentUser());

        var act = () => handler.Handle(
            new CreateFutureTaskCommand(
                directory.Id,
                "Title",
                "Description",
                FutureTaskType.Task,
                null,
                null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateStatus_archives_task_when_row_version_matches()
    {
        var context = new FakeDbContext();
        var task = new FutureTask
        {
            WorkingDirectoryId = Guid.CreateVersion7(),
            Title = "Backlog item",
            Description = "Do something",
            Status = FutureTaskStatus.Open,
            OwnerId = User.SystemUserId
        };
        context.FutureTaskItems.Add(task);
        var handler = new UpdateFutureTaskStatusHandler(context, new FakeCurrentUser());

        var result = await handler.Handle(
            new UpdateFutureTaskStatusCommand(task.Id, FutureTaskStatus.Archived, "0"),
            CancellationToken.None);

        result.Status.Should().Be(FutureTaskStatus.Archived);
        task.Status.Should().Be(FutureTaskStatus.Archived);
        context.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStatus_rejects_stale_row_version()
    {
        var context = new FakeDbContext();
        var task = new FutureTask
        {
            WorkingDirectoryId = Guid.CreateVersion7(),
            Title = "Backlog item",
            Description = "Do something",
            Status = FutureTaskStatus.Open,
            OwnerId = User.SystemUserId
        };
        context.FutureTaskItems.Add(task);
        var handler = new UpdateFutureTaskStatusHandler(context, new FakeCurrentUser());

        var act = () => handler.Handle(
            new UpdateFutureTaskStatusCommand(task.Id, FutureTaskStatus.Done, "999"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        task.Status.Should().Be(FutureTaskStatus.Open);
    }

    [Fact]
    public async Task UpdateStatus_rejects_task_owned_by_another_user()
    {
        var context = new FakeDbContext();
        var task = new FutureTask
        {
            WorkingDirectoryId = Guid.CreateVersion7(),
            Title = "Backlog item",
            Description = "Do something",
            Status = FutureTaskStatus.Open,
            OwnerId = Guid.CreateVersion7()
        };
        context.FutureTaskItems.Add(task);
        var handler = new UpdateFutureTaskStatusHandler(context, new FakeCurrentUser());

        var act = () => handler.Handle(
            new UpdateFutureTaskStatusCommand(task.Id, FutureTaskStatus.Done, "0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("backend", true)]
    [InlineData("priority:high", true)]
    [InlineData("unknown-label", false)]
    public void Validator_enforces_label_preset(string label, bool expectedValid)
    {
        var validator = new CreateFutureTaskValidator();

        var result = validator.Validate(new CreateFutureTaskCommand(
            Guid.CreateVersion7(),
            "Title",
            "Description",
            FutureTaskType.Task,
            new[] { label },
            null));

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Validator_requires_title()
    {
        var validator = new CreateFutureTaskValidator();

        var result = validator.Validate(new CreateFutureTaskCommand(
            Guid.CreateVersion7(),
            "",
            "Description",
            FutureTaskType.Task,
            null,
            null));

        result.IsValid.Should().BeFalse();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class FakeDbContext : IApplicationDbContext
    {
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
        public List<FutureTask> FutureTaskItems { get; } = new();
        public List<FutureTaskLabel> FutureTaskLabelItems { get; } = new();
        public List<Prompt> PromptItems { get; } = new();
        public int SaveChangesCount { get; private set; }

        public IQueryable<User> Users => Array.Empty<User>().AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<FutureTask> FutureTasks => FutureTaskItems.AsQueryable();
        public IQueryable<FutureTaskLabel> FutureTaskLabels => FutureTaskLabelItems.AsQueryable();
        public IQueryable<Prompt> Prompts => PromptItems.AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => Array.Empty<PromptVersion>().AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => Array.Empty<PromptFileReference>().AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => Array.Empty<LinkedDocument>().AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => Array.Empty<LinkedDocumentVersion>().AsQueryable();
        public IQueryable<WorkflowTemplate> WorkflowTemplates => Array.Empty<WorkflowTemplate>().AsQueryable();
        public IQueryable<WorkflowTemplatePhase> WorkflowTemplatePhases => Array.Empty<WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<PromptWorkflow> PromptWorkflows => Array.Empty<PromptWorkflow>().AsQueryable();
        public IQueryable<PromptWorkflowPhase> PromptWorkflowPhases => Array.Empty<PromptWorkflowPhase>().AsQueryable();
        public IQueryable<PromptWorkflowEvent> PromptWorkflowEvents => Array.Empty<PromptWorkflowEvent>().AsQueryable();
        public IQueryable<AiChatSession> AiChatSessions => Array.Empty<AiChatSession>().AsQueryable();
        public IQueryable<AiChatMessage> AiChatMessages => Array.Empty<AiChatMessage>().AsQueryable();
        public IQueryable<AiUserSettings> AiUserSettings => Array.Empty<AiUserSettings>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Notebook> Notebooks => Array.Empty<PromptTasks.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Note> Notes => Array.Empty<PromptTasks.Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Diagrams.Diagram> Diagrams => Array.Empty<PromptTasks.Domain.Diagrams.Diagram>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case FutureTask futureTask:
                    FutureTaskItems.Add(futureTask);
                    break;
                case FutureTaskLabel futureTaskLabel:
                    FutureTaskLabelItems.Add(futureTaskLabel);
                    break;
                case Prompt prompt:
                    PromptItems.Add(prompt);
                    break;
                case WorkingDirectory directory:
                    WorkingDirectoryItems.Add(directory);
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
    }
}

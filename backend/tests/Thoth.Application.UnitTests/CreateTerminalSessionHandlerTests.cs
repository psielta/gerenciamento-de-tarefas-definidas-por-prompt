using FluentAssertions;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;
using Thoth.Application.Features.Terminals;
using Thoth.Application.Features.Terminals.Commands.CreateTerminalSession;
using Thoth.Domain.Prompts;
using Thoth.Domain.Users;
using Thoth.Domain.WorkingDirectories;

namespace Thoth.Application.UnitTests;

public sealed class CreateTerminalSessionHandlerTests
{
    [Fact]
    public async Task Handle_uses_parent_prompt_workspace_for_child_prompts()
    {
        var context = new FakeApplicationDbContext();
        var parentDirectory = SeedWorkingDirectory(context, "D:/parent-repo");
        var childDirectory = SeedWorkingDirectory(context, "D:/child-repo");
        var parentPrompt = SeedPrompt(context, parentDirectory.Id, parentPromptId: null);
        var childPrompt = SeedPrompt(context, childDirectory.Id, parentPrompt.Id);

        var coordinator = new RecordingTerminalCoordinator();
        var handler = new CreateTerminalSessionHandler(context, new FakeCurrentUser(), coordinator);

        await handler.Handle(new CreateTerminalSessionCommand(childPrompt.Id, null, null), CancellationToken.None);

        coordinator.LastCreate.Should().NotBeNull();
        coordinator.LastCreate!.Value.PromptId.Should().Be(childPrompt.Id);
        coordinator.LastCreate.Value.Cwd.Should().Be(parentDirectory.AbsolutePath);
    }

    [Fact]
    public async Task Handle_passes_agent_launch_command_as_initial_input()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context, "D:/repo");
        var prompt = SeedPrompt(context, directory.Id, parentPromptId: null);

        var coordinator = new RecordingTerminalCoordinator();
        var handler = new CreateTerminalSessionHandler(context, new FakeCurrentUser(), coordinator);

        await handler.Handle(
            new CreateTerminalSessionCommand(prompt.Id, null, TerminalAgentLaunch.Claude),
            CancellationToken.None);

        coordinator.LastCreate.Should().NotBeNull();
        coordinator.LastCreate!.Value.InitialInput.Should().BeEquivalentTo(
            "claude --dangerously-skip-permissions --effort max\r"u8.ToArray());
    }

    [Fact]
    public async Task Handle_uses_prompt_workspace_for_root_prompts()
    {
        var context = new FakeApplicationDbContext();
        var directory = SeedWorkingDirectory(context, "D:/repo");
        var prompt = SeedPrompt(context, directory.Id, parentPromptId: null);

        var coordinator = new RecordingTerminalCoordinator();
        var handler = new CreateTerminalSessionHandler(context, new FakeCurrentUser(), coordinator);

        await handler.Handle(new CreateTerminalSessionCommand(prompt.Id, "powershell.exe", null), CancellationToken.None);

        coordinator.LastCreate.Should().NotBeNull();
        coordinator.LastCreate!.Value.PromptId.Should().Be(prompt.Id);
        coordinator.LastCreate.Value.Cwd.Should().Be(directory.AbsolutePath);
        coordinator.LastCreate.Value.Shell.Should().Be("powershell.exe");
    }

    private static WorkingDirectory SeedWorkingDirectory(FakeApplicationDbContext context, string absolutePath)
    {
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = Path.GetFileName(absolutePath.TrimEnd('/')),
            AbsolutePath = absolutePath,
            OwnerId = User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(directory);
        return directory;
    }

    private static Prompt SeedPrompt(FakeApplicationDbContext context, Guid workingDirectoryId, Guid? parentPromptId)
    {
        var prompt = new Prompt
        {
            Id = Guid.CreateVersion7(),
            WorkingDirectoryId = workingDirectoryId,
            ParentPromptId = parentPromptId,
            Title = "Prompt",
            Content = "Content",
            TargetAgent = TargetAgent.ClaudeCode,
            Kind = PromptKind.General,
            Status = PromptStatus.Ready,
            CurrentVersion = 1,
            OwnerId = User.SystemUserId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
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
        public IQueryable<Thoth.Domain.FutureTasks.FutureTask> FutureTasks => Enumerable.Empty<Thoth.Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<Thoth.Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<Thoth.Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Prompt> Prompts => PromptItems.AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => PromptVersionItems.AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => PromptFileReferenceItems.AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => LinkedDocumentItems.AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => LinkedDocumentVersionItems.AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.WorkflowTemplate> WorkflowTemplates => Enumerable.Empty<Thoth.Domain.Workflows.WorkflowTemplate>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.WorkflowTemplatePhase> WorkflowTemplatePhases => Enumerable.Empty<Thoth.Domain.Workflows.WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.PromptWorkflow> PromptWorkflows => Enumerable.Empty<Thoth.Domain.Workflows.PromptWorkflow>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.PromptWorkflowPhase> PromptWorkflowPhases => Enumerable.Empty<Thoth.Domain.Workflows.PromptWorkflowPhase>().AsQueryable();
        public IQueryable<Thoth.Domain.Workflows.PromptWorkflowEvent> PromptWorkflowEvents => Enumerable.Empty<Thoth.Domain.Workflows.PromptWorkflowEvent>().AsQueryable();
        public IQueryable<Thoth.Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<Thoth.Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<Thoth.Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<Thoth.Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<Thoth.Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<Thoth.Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<Thoth.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<Thoth.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<Thoth.Domain.Notebooks.Note> Notes => Enumerable.Empty<Thoth.Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<Thoth.Domain.Diagrams.Diagram> Diagrams => Enumerable.Empty<Thoth.Domain.Diagrams.Diagram>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
        }

        public void Remove<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class RecordingTerminalCoordinator : ITerminalSessionCoordinator
    {
        public (Guid PromptId, string Cwd, string Shell, byte[]? InitialInput)? LastCreate { get; private set; }

        public Task<TerminalSessionDescriptor> CreateAsync(
            Guid promptId,
            string cwd,
            string shell,
            byte[]? initialInput,
            CancellationToken cancellationToken)
        {
            LastCreate = (promptId, cwd, shell, initialInput);
            return Task.FromResult(new TerminalSessionDescriptor(
                Guid.CreateVersion7(),
                promptId,
                shell,
                cwd,
                DateTimeOffset.UtcNow));
        }

        public void WriteInput(Guid sessionId, byte[] input)
        {
        }

        public void Resize(Guid sessionId, ushort cols, ushort rows)
        {
        }

        public Task CloseAsync(Guid sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void AttachConnection(Guid sessionId, string connectionId)
        {
        }

        public void DetachConnection(Guid sessionId, string connectionId)
        {
        }

        public void ReleaseConnection(string connectionId)
        {
        }

        public IReadOnlyList<TerminalSessionDescriptor> ListForPrompt(Guid promptId) =>
            Array.Empty<TerminalSessionDescriptor>();

        public TerminalSessionDescriptor? TryGetSession(Guid sessionId) => null;

        public Task KillForPromptAsync(Guid promptId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Features.Diagrams.Commands.CreateDiagram;
using PromptTasks.Application.Features.Diagrams.Commands.DeleteDiagram;
using PromptTasks.Application.Features.Diagrams.Commands.SetDiagramArchived;
using PromptTasks.Application.Features.Diagrams.Commands.UpdateDiagram;
using PromptTasks.Application.Features.Diagrams.Queries.GetDiagram;
using PromptTasks.Application.Features.Diagrams.Queries.GetDiagrams;
using PromptTasks.Domain.Diagrams;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Application.UnitTests;

public sealed class DiagramHandlerTests
{
    private static readonly Guid OwnerId = User.SystemUserId;
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    [Fact]
    public async Task CreateDiagram_persists_trimmed_fields_for_current_user()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var handler = new CreateDiagramHandler(context, CurrentUser());

        var result = await handler.Handle(
            new CreateDiagramCommand(
                directory.Id,
                "  Arquitetura  ",
                DiagramType.Mermaid,
                "  Fluxo principal  ",
                "graph TD; A-->B"),
            CancellationToken.None);

        result.Title.Should().Be("Arquitetura");
        result.Description.Should().Be("Fluxo principal");
        result.Type.Should().Be(DiagramType.Mermaid);
        result.Content.Should().Be("graph TD; A-->B");
        result.IsArchived.Should().BeFalse();

        context.DiagramItems.Should().ContainSingle();
        context.DiagramItems[0].OwnerId.Should().Be(OwnerId);
        context.DiagramItems[0].WorkingDirectoryId.Should().Be(directory.Id);
    }

    [Fact]
    public async Task CreateDiagram_with_unowned_working_directory_throws_not_found()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OtherUserId);
        var handler = new CreateDiagramHandler(context, CurrentUser());

        var act = () => handler.Handle(
            new CreateDiagramCommand(directory.Id, "Arquitetura", DiagramType.Excalidraw),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.DiagramItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiagrams_filters_by_owner_workspace_archived_and_type()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var otherDirectory = SeedWorkingDirectory(context, OwnerId);

        var excalidraw = SeedDiagram(context, directory.Id, "Board", DiagramType.Excalidraw);
        SeedDiagram(context, directory.Id, "States", DiagramType.Mermaid);
        SeedDiagram(context, directory.Id, "Old", DiagramType.Mermaid, isArchived: true);
        SeedDiagram(context, otherDirectory.Id, "Elsewhere", DiagramType.Mermaid);
        SeedDiagram(context, directory.Id, "Theirs", DiagramType.Mermaid, ownerId: OtherUserId);

        var handler = new GetDiagramsHandler(context, CurrentUser());

        var active = await handler.Handle(new GetDiagramsQuery(directory.Id), CancellationToken.None);
        active.Select(diagram => diagram.Title).Should().BeEquivalentTo("Board", "States");

        var mermaidOnly = await handler.Handle(
            new GetDiagramsQuery(directory.Id, Type: DiagramType.Mermaid),
            CancellationToken.None);
        mermaidOnly.Select(diagram => diagram.Title).Should().BeEquivalentTo("States");

        var withArchived = await handler.Handle(
            new GetDiagramsQuery(directory.Id, IncludeArchived: true),
            CancellationToken.None);
        withArchived.Select(diagram => diagram.Title).Should().Contain("Old");

        // Summary projection never exposes the heavy Content column.
        active.Should().OnlyContain(diagram => diagram.Id != excalidraw.Id || diagram.Title == "Board");
    }

    [Fact]
    public async Task GetDiagrams_searches_title_and_mermaid_content_only()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var mermaid = SeedDiagram(context, directory.Id, "Sequence", DiagramType.Mermaid, content: "sequenceDiagram; Alice->>Bob: oi");
        SeedDiagram(context, directory.Id, "Scene", DiagramType.Excalidraw, content: "{\"elements\":[{\"text\":\"Bob\"}]}");

        var handler = new GetDiagramsHandler(context, CurrentUser());

        var byContent = await handler.Handle(
            new GetDiagramsQuery(directory.Id, Search: "ALICE"),
            CancellationToken.None);
        byContent.Select(diagram => diagram.Id).Should().Equal(mermaid.Id);

        // "Bob" exists in the Excalidraw JSON but content search is Mermaid-only,
        // so only the Mermaid diagram (which also contains "Bob") matches.
        var byMixedTerm = await handler.Handle(
            new GetDiagramsQuery(directory.Id, Search: "bob"),
            CancellationToken.None);
        byMixedTerm.Select(diagram => diagram.Id).Should().Equal(mermaid.Id);
    }

    [Fact]
    public async Task GetDiagrams_without_workspace_lists_across_all_owned_workspaces_with_name()
    {
        var context = new FakeDiagramDbContext();
        var first = SeedWorkingDirectory(context, OwnerId);
        var second = SeedWorkingDirectory(context, OwnerId);
        SeedDiagram(context, first.Id, "Board", DiagramType.Excalidraw);
        SeedDiagram(context, second.Id, "States", DiagramType.Mermaid);
        SeedDiagram(context, first.Id, "Theirs", DiagramType.Mermaid, ownerId: OtherUserId);

        var handler = new GetDiagramsHandler(context, CurrentUser());

        var all = await handler.Handle(new GetDiagramsQuery(), CancellationToken.None);

        all.Select(diagram => diagram.Title).Should().BeEquivalentTo("Board", "States");
        all.Should().OnlyContain(diagram => diagram.WorkingDirectoryName == "repo");
    }

    [Fact]
    public async Task GetDiagram_for_other_user_throws_not_found()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var diagram = SeedDiagram(context, directory.Id, "Theirs", DiagramType.Mermaid, ownerId: OtherUserId);
        var handler = new GetDiagramHandler(context, CurrentUser());

        var act = () => handler.Handle(new GetDiagramQuery(diagram.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateDiagram_updates_content_for_owner_and_rejects_others()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var diagram = SeedDiagram(context, directory.Id, "Board", DiagramType.Excalidraw, content: "{}");
        var handler = new UpdateDiagramHandler(context, CurrentUser());

        var result = await handler.Handle(
            new UpdateDiagramCommand(diagram.Id, "  Board v2  ", "{\"elements\":[]}", "  desc  "),
            CancellationToken.None);

        result.Title.Should().Be("Board v2");
        result.Description.Should().Be("desc");
        result.Content.Should().Be("{\"elements\":[]}");

        var foreign = SeedDiagram(context, directory.Id, "Theirs", DiagramType.Mermaid, ownerId: OtherUserId);
        var act = () => handler.Handle(
            new UpdateDiagramCommand(foreign.Id, "Hacked", "x"),
            CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SetDiagramArchived_toggles_flag_for_owner()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var diagram = SeedDiagram(context, directory.Id, "Board", DiagramType.Excalidraw);
        var handler = new SetDiagramArchivedHandler(context, CurrentUser());

        var archived = await handler.Handle(new SetDiagramArchivedCommand(diagram.Id, true), CancellationToken.None);
        archived.IsArchived.Should().BeTrue();

        var restored = await handler.Handle(new SetDiagramArchivedCommand(diagram.Id, false), CancellationToken.None);
        restored.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDiagram_removes_for_owner_and_rejects_others()
    {
        var context = new FakeDiagramDbContext();
        var directory = SeedWorkingDirectory(context, OwnerId);
        var diagram = SeedDiagram(context, directory.Id, "Board", DiagramType.Excalidraw);
        var foreign = SeedDiagram(context, directory.Id, "Theirs", DiagramType.Mermaid, ownerId: OtherUserId);
        var handler = new DeleteDiagramHandler(context, CurrentUser());

        await handler.Handle(new DeleteDiagramCommand(diagram.Id), CancellationToken.None);
        context.DiagramItems.Should().ContainSingle(item => item.Id == foreign.Id);

        var act = () => handler.Handle(new DeleteDiagramCommand(foreign.Id), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static FakeCurrentUser CurrentUser() => new(OwnerId);

    private static WorkingDirectory SeedWorkingDirectory(FakeDiagramDbContext context, Guid ownerId)
    {
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = ownerId
        };
        context.WorkingDirectoryItems.Add(directory);
        return directory;
    }

    private static Diagram SeedDiagram(
        FakeDiagramDbContext context,
        Guid workingDirectoryId,
        string title,
        DiagramType type,
        string content = "",
        bool isArchived = false,
        Guid? ownerId = null)
    {
        var diagram = new Diagram
        {
            Id = Guid.CreateVersion7(),
            WorkingDirectoryId = workingDirectoryId,
            Title = title,
            Type = type,
            Content = content,
            IsArchived = isArchived,
            OwnerId = ownerId ?? OwnerId,
            CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)
        };
        context.DiagramItems.Add(diagram);
        return diagram;
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeDiagramDbContext : IApplicationDbContext
    {
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
        public List<Diagram> DiagramItems { get; } = new();

        public IQueryable<User> Users => Enumerable.Empty<User>().AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<Domain.FutureTasks.FutureTask> FutureTasks => Enumerable.Empty<Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Domain.Prompts.Prompt> Prompts => Enumerable.Empty<Domain.Prompts.Prompt>().AsQueryable();
        public IQueryable<Domain.Prompts.PromptVersion> PromptVersions => Enumerable.Empty<Domain.Prompts.PromptVersion>().AsQueryable();
        public IQueryable<Domain.Prompts.PromptFileReference> PromptFileReferences => Enumerable.Empty<Domain.Prompts.PromptFileReference>().AsQueryable();
        public IQueryable<Domain.Prompts.LinkedDocument> LinkedDocuments => Enumerable.Empty<Domain.Prompts.LinkedDocument>().AsQueryable();
        public IQueryable<Domain.Prompts.LinkedDocumentVersion> LinkedDocumentVersions => Enumerable.Empty<Domain.Prompts.LinkedDocumentVersion>().AsQueryable();
        public IQueryable<Domain.Workflows.WorkflowTemplate> WorkflowTemplates => Enumerable.Empty<Domain.Workflows.WorkflowTemplate>().AsQueryable();
        public IQueryable<Domain.Workflows.WorkflowTemplatePhase> WorkflowTemplatePhases => Enumerable.Empty<Domain.Workflows.WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<Domain.Workflows.PromptWorkflow> PromptWorkflows => Enumerable.Empty<Domain.Workflows.PromptWorkflow>().AsQueryable();
        public IQueryable<Domain.Workflows.PromptWorkflowPhase> PromptWorkflowPhases => Enumerable.Empty<Domain.Workflows.PromptWorkflowPhase>().AsQueryable();
        public IQueryable<Domain.Workflows.PromptWorkflowEvent> PromptWorkflowEvents => Enumerable.Empty<Domain.Workflows.PromptWorkflowEvent>().AsQueryable();
        public IQueryable<Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<Domain.Notebooks.Note> Notes => Enumerable.Empty<Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<Diagram> Diagrams => DiagramItems.AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case Diagram diagram:
                    DiagramItems.Add(diagram);
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
                case Diagram diagram:
                    DiagramItems.Remove(diagram);
                    break;
                case WorkingDirectory directory:
                    WorkingDirectoryItems.Remove(directory);
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
}

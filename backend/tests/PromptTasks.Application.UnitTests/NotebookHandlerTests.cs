using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Features.Notebooks.Commands.CreateNotebook;
using PromptTasks.Application.Features.Notebooks.Commands.DeleteNotebook;
using PromptTasks.Application.Features.Notebooks.Commands.SetNotebookArchived;
using PromptTasks.Application.Features.Notebooks.Commands.UpdateNotebook;
using PromptTasks.Application.Features.Notebooks.Queries.GetNotebook;
using PromptTasks.Application.Features.Notebooks.Queries.GetNotebooks;
using PromptTasks.Application.Features.Notes.Commands.CreateNote;
using PromptTasks.Application.Features.Notes.Commands.DeleteNote;
using PromptTasks.Application.Features.Notes.Commands.SetNoteArchived;
using PromptTasks.Application.Features.Notes.Commands.SetNotePinned;
using PromptTasks.Application.Features.Notes.Commands.UpdateNote;
using PromptTasks.Application.Features.Notes.Queries.GetNotes;
using PromptTasks.Domain.Notebooks;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Application.UnitTests;

public sealed class NotebookHandlerTests
{
    private static readonly Guid OwnerId = User.SystemUserId;
    private static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    [Fact]
    public async Task CreateNotebook_persists_trimmed_fields_for_current_user()
    {
        var context = new FakeNotebookDbContext();
        var handler = new CreateNotebookHandler(context, CurrentUser());

        var result = await handler.Handle(
            new CreateNotebookCommand("  Ideias  ", "  Anotacoes soltas  "),
            CancellationToken.None);

        result.Title.Should().Be("Ideias");
        result.Description.Should().Be("Anotacoes soltas");
        result.IsArchived.Should().BeFalse();
        result.NoteCount.Should().Be(0);

        context.NotebookItems.Should().ContainSingle();
        context.NotebookItems[0].OwnerId.Should().Be(OwnerId);
    }

    [Fact]
    public async Task CreateNotebook_with_unowned_working_directory_throws_not_found()
    {
        var context = new FakeNotebookDbContext();
        context.WorkingDirectoryItems.Add(new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = OtherUserId
        });
        var handler = new CreateNotebookHandler(context, CurrentUser());

        var act = () => handler.Handle(
            new CreateNotebookCommand("Ideias", null, context.WorkingDirectoryItems[0].Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.NotebookItems.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateNotebook_links_owned_working_directory_and_exposes_name()
    {
        var context = new FakeNotebookDbContext();
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = OwnerId
        };
        context.WorkingDirectoryItems.Add(directory);
        var handler = new CreateNotebookHandler(context, CurrentUser());

        var result = await handler.Handle(
            new CreateNotebookCommand("Notas do repo", null, directory.Id),
            CancellationToken.None);

        result.WorkingDirectoryId.Should().Be(directory.Id);
        result.WorkingDirectoryName.Should().Be("repo");
    }

    [Fact]
    public async Task GetNotebooks_returns_only_owner_notebooks_with_note_counts()
    {
        var context = new FakeNotebookDbContext();
        var mine = SeedNotebook(context, "Mine", OwnerId);
        var archived = SeedNotebook(context, "Archived", OwnerId, isArchived: true);
        SeedNotebook(context, "Theirs", OtherUserId);
        SeedNote(context, mine.Id, "Active");
        SeedNote(context, mine.Id, "Archived note", isArchived: true);

        var handler = new GetNotebooksHandler(context, CurrentUser());

        var active = await handler.Handle(new GetNotebooksQuery(IncludeArchived: false), CancellationToken.None);
        active.Should().ContainSingle();
        active[0].Title.Should().Be("Mine");
        active[0].NoteCount.Should().Be(1); // archived note is excluded from the count

        var all = await handler.Handle(new GetNotebooksQuery(IncludeArchived: true), CancellationToken.None);
        all.Select(notebook => notebook.Title).Should().BeEquivalentTo(new[] { "Mine", "Archived" });
        all.Should().NotContain(notebook => notebook.Id == archived.Id && notebook.IsArchived == false);
    }

    [Fact]
    public async Task UpdateNotebook_for_other_user_throws_not_found()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Theirs", OtherUserId);
        var handler = new UpdateNotebookHandler(context, CurrentUser());

        var act = () => handler.Handle(
            new UpdateNotebookCommand(notebook.Id, "Hacked"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        notebook.Title.Should().Be("Theirs");
    }

    [Fact]
    public async Task SetNotebookArchived_toggles_flag()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Mine", OwnerId);
        var handler = new SetNotebookArchivedHandler(context, CurrentUser());

        var result = await handler.Handle(new SetNotebookArchivedCommand(notebook.Id, true), CancellationToken.None);

        result.IsArchived.Should().BeTrue();
        notebook.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteNotebook_removes_notebook_and_its_notes()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Mine", OwnerId);
        SeedNote(context, notebook.Id, "Keep me out of orphans");
        var handler = new DeleteNotebookHandler(context, CurrentUser());

        await handler.Handle(new DeleteNotebookCommand(notebook.Id), CancellationToken.None);

        context.NotebookItems.Should().BeEmpty();
        context.NoteItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotebook_for_missing_id_throws_not_found()
    {
        var context = new FakeNotebookDbContext();
        var handler = new GetNotebookHandler(context, CurrentUser());

        var act = () => handler.Handle(new GetNotebookQuery(Guid.CreateVersion7()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateNote_requires_an_owned_notebook()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Theirs", OtherUserId);
        var handler = new CreateNoteHandler(context, CurrentUser());

        var act = () => handler.Handle(
            new CreateNoteCommand(notebook.Id, "Note"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.NoteItems.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateNote_persists_with_owner_and_default_content()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Mine", OwnerId);
        var handler = new CreateNoteHandler(context, CurrentUser());

        var result = await handler.Handle(
            new CreateNoteCommand(notebook.Id, "  Primeira nota  "),
            CancellationToken.None);

        result.Title.Should().Be("Primeira nota");
        result.ContentMarkdown.Should().BeEmpty();
        context.NoteItems.Should().ContainSingle();
        context.NoteItems[0].OwnerId.Should().Be(OwnerId);
    }

    [Fact]
    public async Task UpdateNote_changes_title_and_content()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Mine", OwnerId);
        var note = SeedNote(context, notebook.Id, "Old");
        var handler = new UpdateNoteHandler(context, CurrentUser());

        var result = await handler.Handle(
            new UpdateNoteCommand(note.Id, "New", "# Conteudo"),
            CancellationToken.None);

        result.Title.Should().Be("New");
        result.ContentMarkdown.Should().Be("# Conteudo");
    }

    [Fact]
    public async Task SetNotePinned_and_SetNoteArchived_toggle_flags()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Mine", OwnerId);
        var note = SeedNote(context, notebook.Id, "Note");

        var pinned = await new SetNotePinnedHandler(context, CurrentUser())
            .Handle(new SetNotePinnedCommand(note.Id, true), CancellationToken.None);
        pinned.IsPinned.Should().BeTrue();

        var archived = await new SetNoteArchivedHandler(context, CurrentUser())
            .Handle(new SetNoteArchivedCommand(note.Id, true), CancellationToken.None);
        archived.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetNotes_filters_by_notebook_search_and_orders_pinned_first()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Mine", OwnerId);
        var other = SeedNotebook(context, "Other", OwnerId);
        SeedNote(context, notebook.Id, "Plain note", content: "talk about postgres",
            updatedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        SeedNote(context, notebook.Id, "Pinned idea", content: "redis cache", isPinned: true,
            updatedAt: new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero));
        SeedNote(context, notebook.Id, "Archived", isArchived: true);
        SeedNote(context, other.Id, "Different notebook");

        var handler = new GetNotesHandler(context, CurrentUser());

        var inNotebook = await handler.Handle(new GetNotesQuery(NotebookId: notebook.Id), CancellationToken.None);
        inNotebook.Select(note => note.Title).Should().ContainInOrder("Pinned idea", "Plain note");
        inNotebook.Should().NotContain(note => note.Title == "Archived");
        inNotebook.Should().NotContain(note => note.Title == "Different notebook");

        var search = await handler.Handle(
            new GetNotesQuery(NotebookId: notebook.Id, Search: "POSTGRES"),
            CancellationToken.None);
        search.Should().ContainSingle(note => note.Title == "Plain note");
    }

    [Fact]
    public async Task DeleteNote_for_other_user_throws_not_found()
    {
        var context = new FakeNotebookDbContext();
        var notebook = SeedNotebook(context, "Theirs", OtherUserId);
        var note = SeedNote(context, notebook.Id, "Note", ownerId: OtherUserId);
        var handler = new DeleteNoteHandler(context, CurrentUser());

        var act = () => handler.Handle(new DeleteNoteCommand(note.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        context.NoteItems.Should().ContainSingle();
    }

    private static FakeCurrentUser CurrentUser() => new(OwnerId);

    private static Notebook SeedNotebook(FakeNotebookDbContext context, string title, Guid ownerId, bool isArchived = false)
    {
        var notebook = new Notebook
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            OwnerId = ownerId,
            IsArchived = isArchived,
            CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)
        };
        context.NotebookItems.Add(notebook);
        return notebook;
    }

    private static Note SeedNote(
        FakeNotebookDbContext context,
        Guid notebookId,
        string title,
        string content = "",
        bool isPinned = false,
        bool isArchived = false,
        Guid? ownerId = null,
        DateTimeOffset? updatedAt = null)
    {
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            NotebookId = notebookId,
            Title = title,
            ContentMarkdown = content,
            IsPinned = isPinned,
            IsArchived = isArchived,
            OwnerId = ownerId ?? OwnerId,
            CreatedAtUtc = updatedAt ?? new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = updatedAt ?? new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)
        };
        context.NoteItems.Add(note);
        return note;
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeNotebookDbContext : IApplicationDbContext
    {
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
        public List<Notebook> NotebookItems { get; } = new();
        public List<Note> NoteItems { get; } = new();

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
        public IQueryable<Notebook> Notebooks => NotebookItems.AsQueryable();
        public IQueryable<Note> Notes => NoteItems.AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case Notebook notebook:
                    NotebookItems.Add(notebook);
                    break;
                case Note note:
                    NoteItems.Add(note);
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
                case Notebook notebook:
                    NotebookItems.Remove(notebook);
                    break;
                case Note note:
                    NoteItems.Remove(note);
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

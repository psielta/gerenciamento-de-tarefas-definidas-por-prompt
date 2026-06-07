using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Api.IntegrationTests;

public sealed class NotebookApiTests(PromptTasksApiFactory factory) : IClassFixture<PromptTasksApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Notebook_flow_creates_lists_searches_pins_archives_and_deletes()
    {
        var client = factory.CreateClient();

        // Create a notebook.
        var createNotebook = await client.PostAsJsonAsync(
            "/api/notebooks",
            new { title = "Ideias de produto", description = "Coisas para investigar" },
            JsonOptions);
        createNotebook.StatusCode.Should().Be(HttpStatusCode.Created, await createNotebook.Content.ReadAsStringAsync());
        var notebook = await createNotebook.Content.ReadFromJsonAsync<NotebookDto>(JsonOptions);
        notebook.Should().NotBeNull();
        notebook!.Title.Should().Be("Ideias de produto");
        notebook.NoteCount.Should().Be(0);
        notebook.IsArchived.Should().BeFalse();

        // It shows up in the list.
        var notebooks = await client.GetFromJsonAsync<NotebookDto[]>("/api/notebooks", JsonOptions);
        notebooks.Should().Contain(item => item.Id == notebook.Id);

        // Create a note inside it.
        var createNote = await client.PostAsJsonAsync(
            "/api/notes",
            new { notebookId = notebook.Id, title = "Investigar SignalR", contentMarkdown = "# Plano\n- testar reconexao" },
            JsonOptions);
        createNote.StatusCode.Should().Be(HttpStatusCode.Created, await createNote.Content.ReadAsStringAsync());
        var note = await createNote.Content.ReadFromJsonAsync<NoteDto>(JsonOptions);
        note!.NotebookId.Should().Be(notebook.Id);
        note.ContentMarkdown.Should().Contain("testar reconexao");

        // The notebook now reports a note count of 1.
        var refreshedNotebook = await client.GetFromJsonAsync<NotebookDto>($"/api/notebooks/{notebook.Id}", JsonOptions);
        refreshedNotebook!.NoteCount.Should().Be(1);

        // The note is listed under its notebook.
        var notes = await client.GetFromJsonAsync<NoteDto[]>($"/api/notes?notebookId={notebook.Id}", JsonOptions);
        notes.Should().ContainSingle(item => item.Id == note.Id);

        // Update the note.
        var updateNote = await client.PutAsJsonAsync(
            $"/api/notes/{note.Id}",
            new { title = "Investigar SignalR a fundo", contentMarkdown = "## Atualizado" },
            JsonOptions);
        updateNote.EnsureSuccessStatusCode();
        var updatedNote = await updateNote.Content.ReadFromJsonAsync<NoteDto>(JsonOptions);
        updatedNote!.Title.Should().Be("Investigar SignalR a fundo");
        updatedNote.ContentMarkdown.Should().Be("## Atualizado");

        // Pin it.
        var pin = await client.PostAsJsonAsync($"/api/notes/{note.Id}/pin", new { isPinned = true }, JsonOptions);
        pin.EnsureSuccessStatusCode();
        (await pin.Content.ReadFromJsonAsync<NoteDto>(JsonOptions))!.IsPinned.Should().BeTrue();

        // Search by content (case-insensitive).
        var search = await client.GetFromJsonAsync<NoteDto[]>(
            $"/api/notes?notebookId={notebook.Id}&q=ATUALIZADO",
            JsonOptions);
        search.Should().ContainSingle(item => item.Id == note.Id);

        // Archive the note: it disappears from the default list but appears with includeArchived.
        var archiveNote = await client.PostAsJsonAsync($"/api/notes/{note.Id}/archive", new { isArchived = true }, JsonOptions);
        archiveNote.EnsureSuccessStatusCode();

        var activeNotes = await client.GetFromJsonAsync<NoteDto[]>($"/api/notes?notebookId={notebook.Id}", JsonOptions);
        activeNotes.Should().NotContain(item => item.Id == note.Id);

        var allNotes = await client.GetFromJsonAsync<NoteDto[]>(
            $"/api/notes?notebookId={notebook.Id}&includeArchived=true",
            JsonOptions);
        allNotes.Should().Contain(item => item.Id == note.Id);

        // Archive the notebook: it disappears from the default list.
        var archiveNotebook = await client.PostAsJsonAsync(
            $"/api/notebooks/{notebook.Id}/archive",
            new { isArchived = true },
            JsonOptions);
        archiveNotebook.EnsureSuccessStatusCode();

        var activeNotebooks = await client.GetFromJsonAsync<NotebookDto[]>("/api/notebooks", JsonOptions);
        activeNotebooks.Should().NotContain(item => item.Id == notebook.Id);

        var allNotebooks = await client.GetFromJsonAsync<NotebookDto[]>("/api/notebooks?includeArchived=true", JsonOptions);
        allNotebooks.Should().Contain(item => item.Id == notebook.Id);

        // Delete the note, then the notebook.
        (await client.DeleteAsync($"/api/notes/{note.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/notebooks/{notebook.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/notebooks/{notebook.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_note_in_a_missing_notebook_returns_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/notes",
            new { notebookId = Guid.NewGuid(), title = "Orphan" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_notebook_without_a_title_returns_bad_request()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/notebooks",
            new { title = "" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

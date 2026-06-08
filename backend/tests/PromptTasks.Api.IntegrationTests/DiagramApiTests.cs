using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Api.IntegrationTests;

public sealed class DiagramApiTests(PromptTasksApiFactory factory) : IClassFixture<PromptTasksApiFactory>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"prompttasks-diagrams-{Guid.NewGuid():N}");

    [Fact]
    public async Task Diagram_flow_creates_lists_searches_updates_archives_and_deletes()
    {
        var client = factory.CreateClient();
        var workspaceId = await CreateWorkspaceAsync(client);

        // Create an Excalidraw diagram.
        var createExcalidraw = await client.PostAsJsonAsync(
            "/api/diagrams",
            new
            {
                workingDirectoryId = workspaceId,
                title = "Board",
                type = DiagramType.Excalidraw,
                content = "{\"type\":\"excalidraw\",\"elements\":[]}"
            },
            JsonOptions);
        createExcalidraw.StatusCode.Should().Be(HttpStatusCode.Created, await createExcalidraw.Content.ReadAsStringAsync());
        var excalidraw = await createExcalidraw.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);
        excalidraw!.Type.Should().Be(DiagramType.Excalidraw);
        excalidraw.IsArchived.Should().BeFalse();

        // Create a Mermaid diagram.
        var createMermaid = await client.PostAsJsonAsync(
            "/api/diagrams",
            new
            {
                workingDirectoryId = workspaceId,
                title = "States",
                type = DiagramType.Mermaid,
                content = "stateDiagram-v2; [*] --> Idle"
            },
            JsonOptions);
        createMermaid.StatusCode.Should().Be(HttpStatusCode.Created, await createMermaid.Content.ReadAsStringAsync());
        var mermaid = await createMermaid.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);

        // Both appear in the workspace list.
        var list = await client.GetFromJsonAsync<DiagramSummaryDto[]>(
            $"/api/diagrams?workingDirectoryId={workspaceId}",
            JsonOptions);
        list.Should().Contain(item => item.Id == excalidraw.Id);
        list.Should().Contain(item => item.Id == mermaid!.Id);

        // Filter by type.
        var mermaidOnly = await client.GetFromJsonAsync<DiagramSummaryDto[]>(
            $"/api/diagrams?workingDirectoryId={workspaceId}&type=Mermaid",
            JsonOptions);
        mermaidOnly.Should().OnlyContain(item => item.Type == DiagramType.Mermaid);
        mermaidOnly.Should().Contain(item => item.Id == mermaid!.Id);

        // Search hits Mermaid source (case-insensitive).
        var search = await client.GetFromJsonAsync<DiagramSummaryDto[]>(
            $"/api/diagrams?workingDirectoryId={workspaceId}&q=IDLE",
            JsonOptions);
        search.Should().ContainSingle(item => item.Id == mermaid!.Id);

        // Detail returns the full content.
        var detail = await client.GetFromJsonAsync<DiagramDto>($"/api/diagrams/{excalidraw.Id}", JsonOptions);
        detail!.Content.Should().Contain("excalidraw");

        // Update the Excalidraw scene.
        var update = await client.PutAsJsonAsync(
            $"/api/diagrams/{excalidraw.Id}",
            new { title = "Board v2", content = "{\"type\":\"excalidraw\",\"elements\":[{\"id\":\"a\"}]}", description = "fluxo" },
            JsonOptions);
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);
        updated!.Title.Should().Be("Board v2");
        updated.Description.Should().Be("fluxo");
        updated.Content.Should().Contain("\"id\":\"a\"");

        // Archive: disappears from the default list, appears with includeArchived.
        var archive = await client.PostAsJsonAsync(
            $"/api/diagrams/{excalidraw.Id}/archive",
            new { isArchived = true },
            JsonOptions);
        archive.EnsureSuccessStatusCode();

        var active = await client.GetFromJsonAsync<DiagramSummaryDto[]>(
            $"/api/diagrams?workingDirectoryId={workspaceId}",
            JsonOptions);
        active.Should().NotContain(item => item.Id == excalidraw.Id);

        var withArchived = await client.GetFromJsonAsync<DiagramSummaryDto[]>(
            $"/api/diagrams?workingDirectoryId={workspaceId}&includeArchived=true",
            JsonOptions);
        withArchived.Should().Contain(item => item.Id == excalidraw.Id);

        // Delete permanently.
        (await client.DeleteAsync($"/api/diagrams/{excalidraw.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/diagrams/{excalidraw.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_diagram_in_a_missing_workspace_returns_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/diagrams",
            new { workingDirectoryId = Guid.NewGuid(), title = "Orphan", type = DiagramType.Mermaid },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_diagram_without_a_workspace_returns_bad_request()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/diagrams",
            new { workingDirectoryId = Guid.Empty, title = "No workspace", type = DiagramType.Mermaid },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listing_diagrams_without_a_workspace_returns_all_owned_diagrams_with_workspace_name()
    {
        var client = factory.CreateClient();
        var workspaceId = await CreateWorkspaceAsync(client);

        var create = await client.PostAsJsonAsync(
            "/api/diagrams",
            new { workingDirectoryId = workspaceId, title = "Global board", type = DiagramType.Excalidraw },
            JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var diagram = await create.Content.ReadFromJsonAsync<DiagramDto>(JsonOptions);

        // No workspace filter: the global /diagramas list returns the user's diagrams
        // across every workspace, each carrying its workspace name.
        var all = await client.GetFromJsonAsync<DiagramSummaryDto[]>("/api/diagrams", JsonOptions);
        all.Should().Contain(item => item.Id == diagram!.Id);
        all!.Single(item => item.Id == diagram!.Id).WorkingDirectoryName.Should().Be("repo");
    }

    private async Task<Guid> CreateWorkspaceAsync(HttpClient client)
    {
        Directory.CreateDirectory(_tempRoot);

        var response = await client.PostAsJsonAsync(
            "/api/working-directories",
            new { name = "repo", absolutePath = _tempRoot, respectGitignore = true },
            JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var workspace = await response.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);
        return workspace!.Id;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}

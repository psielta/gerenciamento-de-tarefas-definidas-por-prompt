using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Api.IntegrationTests;

public sealed class TaskNumberingApiTests(PromptTasksApiFactory factory) : IClassFixture<PromptTasksApiFactory>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"prompttasks-numbering-{Guid.NewGuid():N}");

    [Fact]
    public async Task Root_prompts_get_distinct_daily_task_numbers_under_concurrency()
    {
        factory.Clock.Set(new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero));
        var client = factory.CreateClient();
        var workspace = await CreateWorkspaceAsync(client, "BP{N:000}{Date}");

        var tasks = Enumerable.Range(1, 8)
            .Select(index => CreatePromptAsync(client, workspace.Id, $"Task {index}"))
            .ToArray();

        var prompts = await Task.WhenAll(tasks);

        prompts.Select(prompt => prompt.TaskNumber)
            .Should()
            .BeEquivalentTo(Enumerable.Range(1, 8).Select(index => $"BP{index:000}280526"));
        prompts.Should().OnlyContain(prompt => prompt.ParentPromptId == null);

        var lookup = await client.GetFromJsonAsync<PromptDto>(
            $"/api/prompts/by-task-number?workingDirectoryId={workspace.Id}&taskNumber={prompts[0].TaskNumber}",
            JsonOptions);
        lookup!.Id.Should().Be(prompts[0].Id);

        var list = await client.GetFromJsonAsync<PromptDto[]>(
            $"/api/prompts?workingDirectoryId={workspace.Id}&rootOnly=true&q={prompts[0].TaskNumber}",
            JsonOptions);
        list.Should().ContainSingle(prompt => prompt.Id == prompts[0].Id);

        var board = await client.GetFromJsonAsync<TaskSummaryDto[]>(
            $"/api/workflow/board?workingDirectoryId={workspace.Id}&q={prompts[0].TaskNumber}",
            JsonOptions);
        board.Should().ContainSingle(summary => summary.PromptId == prompts[0].Id && summary.TaskNumber == prompts[0].TaskNumber);
    }

    [Fact]
    public async Task Daily_sequence_resets_and_child_prompts_are_not_numbered()
    {
        var client = factory.CreateClient();
        var workspace = await CreateWorkspaceAsync(client, "TASK-{N}-{Date:yyyyMMdd}");

        factory.Clock.Set(new DateTimeOffset(2026, 5, 28, 10, 0, 0, TimeSpan.Zero));
        var first = await CreatePromptAsync(client, workspace.Id, "First root");
        first.TaskNumber.Should().Be("TASK-1-20260528");

        var child = await CreatePromptAsync(client, workspace.Id, "Child prompt", first.Id);
        child.TaskNumber.Should().BeNull();

        factory.Clock.Set(new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero));
        var nextDay = await CreatePromptAsync(client, workspace.Id, "Next day root");
        nextDay.TaskNumber.Should().Be("TASK-1-20260529");
    }

    [Fact]
    public async Task Workspace_update_preserves_or_disables_pattern_based_on_optional_payload()
    {
        var client = factory.CreateClient();
        var workspace = await CreateWorkspaceAsync(client, "BP{N}{Date}");

        var preserveResponse = await client.PutAsync(
            $"/api/working-directories/{workspace.Id}",
            JsonContent(new
            {
                name = workspace.Name,
                absolutePath = workspace.AbsolutePath,
                respectGitignore = workspace.RespectGitignore,
                enableAiContext = true
            }));
        preserveResponse.StatusCode.Should().Be(HttpStatusCode.OK, await preserveResponse.Content.ReadAsStringAsync());
        var preserved = await preserveResponse.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);
        preserved!.TaskNumberPattern.Should().Be("BP{N}{Date}");
        preserved.EnableAiContext.Should().BeTrue();

        var disableResponse = await client.PutAsync(
            $"/api/working-directories/{workspace.Id}",
            JsonContent(new
            {
                name = workspace.Name,
                absolutePath = workspace.AbsolutePath,
                respectGitignore = workspace.RespectGitignore,
                enableAiContext = true,
                taskNumberPattern = ""
            }));
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK, await disableResponse.Content.ReadAsStringAsync());
        var disabled = await disableResponse.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);
        disabled!.TaskNumberPattern.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_pattern_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/working-directories",
            JsonContent(new
            {
                name = "invalid",
                absolutePath = CreateWorkingDirectoryPath(),
                respectGitignore = true,
                taskNumberPattern = "BP/{N}{Date}"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private async Task<WorkingDirectoryDto> CreateWorkspaceAsync(HttpClient client, string? taskNumberPattern)
    {
        var response = await client.PostAsync(
            "/api/working-directories",
            JsonContent(new
            {
                name = $"repo-{Guid.NewGuid():N}",
                absolutePath = CreateWorkingDirectoryPath(),
                respectGitignore = true,
                taskNumberPattern
            }));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions))!;
    }

    private static async Task<PromptDto> CreatePromptAsync(
        HttpClient client,
        Guid workingDirectoryId,
        string title,
        Guid? parentPromptId = null)
    {
        var response = await client.PostAsync(
            "/api/prompts",
            JsonContent(new
            {
                workingDirectoryId,
                parentPromptId,
                title,
                content = "Task content",
                targetAgent = "Codex",
                kind = "General",
                status = "Draft",
                mentions = Array.Empty<object>()
            }));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PromptDto>(JsonOptions))!;
    }

    private string CreateWorkingDirectoryPath()
    {
        var path = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}

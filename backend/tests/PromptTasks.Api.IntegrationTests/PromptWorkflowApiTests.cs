using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Api.IntegrationTests;

public sealed class PromptWorkflowApiTests(PromptTasksApiFactory factory) : IClassFixture<PromptTasksApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static string CreateWorkingDirectoryPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private async Task<PromptDto> CreateRootPromptAsync(HttpClient client, string title)
    {
        var createDirectory = await client.PostAsync(
            "/api/working-directories",
            JsonContent(new { name = "repo", absolutePath = CreateWorkingDirectoryPath(), respectGitignore = true }));
        createDirectory.StatusCode.Should().Be(HttpStatusCode.Created, await createDirectory.Content.ReadAsStringAsync());
        var directory = await createDirectory.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);

        var createPrompt = await client.PostAsync(
            "/api/prompts",
            JsonContent(new
            {
                workingDirectoryId = directory!.Id,
                title,
                content = "conteúdo",
                targetAgent = "Codex",
                kind = "Planning",
                status = "Draft",
                mentions = Array.Empty<object>()
            }));
        createPrompt.StatusCode.Should().Be(HttpStatusCode.Created, await createPrompt.Content.ReadAsStringAsync());
        var prompt = await createPrompt.Content.ReadFromJsonAsync<PromptDto>(JsonOptions);
        return prompt!;
    }

    private async Task<WorkflowDto> PostWorkflowAsync(HttpClient client, Guid promptId, string action, object payload)
    {
        var response = await client.PostAsync($"/api/prompts/{promptId}/workflow/{action}", JsonContent(payload));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var dto = await response.Content.ReadFromJsonAsync<WorkflowDto>(JsonOptions);
        return dto!;
    }

    [Fact]
    public async Task Full_workflow_lifecycle_via_api()
    {
        var client = factory.CreateClient();
        var prompt = await CreateRootPromptAsync(client, "Tarefa do dia");

        var workflow = await client.GetFromJsonAsync<WorkflowDto>($"/api/prompts/{prompt.Id}/workflow", JsonOptions);
        workflow.Should().NotBeNull();
        workflow!.Status.Should().Be(PromptWorkflowStatus.Active);
        workflow.CurrentPhaseName.Should().Be("Engenharia de prompt");
        workflow.CurrentActor.Should().Be(WorkflowActor.Human);
        workflow.CurrentPhaseIteration.Should().Be(1);
        workflow.Phases.Should().Contain(phase => phase.Name == "Revisão do plano");

        workflow = await PostWorkflowAsync(client, prompt.Id, "advance", new { rowVersion = workflow.RowVersion, note = (string?)null });
        workflow.CurrentPhaseName.Should().Be("Planejamento");
        workflow.CurrentActor.Should().Be(WorkflowActor.ClaudeCode);
        workflow = await PostWorkflowAsync(client, prompt.Id, "advance", new { rowVersion = workflow.RowVersion, note = (string?)null });
        workflow.CurrentPhaseName.Should().Be("Revisão do plano");
        workflow = await PostWorkflowAsync(client, prompt.Id, "advance", new { rowVersion = workflow.RowVersion, note = (string?)null });
        workflow.CurrentPhaseName.Should().Be("Correção do plano");

        var reviewPhaseId = workflow.Phases.Single(phase => phase.Name == "Revisão do plano").Id;
        workflow = await PostWorkflowAsync(client, prompt.Id, "phase",
            new { phaseId = reviewPhaseId, actor = (string?)null, note = (string?)null, rowVersion = workflow.RowVersion });
        workflow.CurrentPhaseName.Should().Be("Revisão do plano");

        var conflict = await client.PostAsync(
            $"/api/prompts/{prompt.Id}/workflow/advance",
            JsonContent(new { rowVersion = "999999", note = (string?)null }));
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        workflow = await PostWorkflowAsync(client, prompt.Id, "notes", new { note = "Codex pediu ajustes" });
        workflow.Events.Should().Contain(@event => @event.Type == WorkflowEventType.Note && @event.Note == "Codex pediu ajustes");

        workflow = await PostWorkflowAsync(client, prompt.Id, "complete", new { note = (string?)null, rowVersion = workflow.RowVersion });
        workflow.Status.Should().Be(PromptWorkflowStatus.Done);
        workflow = await PostWorkflowAsync(client, prompt.Id, "reopen", new { phaseId = (Guid?)null, rowVersion = workflow.RowVersion });
        workflow.Status.Should().Be(PromptWorkflowStatus.Active);

        var board = await client.GetFromJsonAsync<List<TaskSummaryDto>>("/api/workflow/board", JsonOptions);
        board.Should().Contain(summary => summary.PromptId == prompt.Id && summary.CurrentPhaseName == workflow.CurrentPhaseName);
        var boardSummary = board!.Single(summary => summary.PromptId == prompt.Id);
        boardSummary.Phases.Should().Contain(phase => phase.Name == "Revisão do plano");
        boardSummary.PromptRowVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Board_hides_archived_prompts_by_default()
    {
        var client = factory.CreateClient();
        var prompt = await CreateRootPromptAsync(client, "Tarefa arquivável");

        var rowVersion = (await client.GetFromJsonAsync<PromptDto>($"/api/prompts/{prompt.Id}", JsonOptions))!.RowVersion;
        var archive = await client.PatchAsync(
            $"/api/prompts/{prompt.Id}/status",
            JsonContent(new { status = "Archived", rowVersion }));
        archive.StatusCode.Should().Be(HttpStatusCode.OK, await archive.Content.ReadAsStringAsync());

        var defaultBoard = await client.GetFromJsonAsync<List<TaskSummaryDto>>("/api/workflow/board", JsonOptions);
        defaultBoard.Should().NotContain(summary => summary.PromptId == prompt.Id);

        var archivedBoard = await client.GetFromJsonAsync<List<TaskSummaryDto>>("/api/workflow/board?promptStatus=Archived", JsonOptions);
        archivedBoard.Should().Contain(summary => summary.PromptId == prompt.Id);
    }

    [Fact]
    public async Task Editing_template_applies_to_new_tasks()
    {
        var client = factory.CreateClient();
        var template = await client.GetFromJsonAsync<WorkflowTemplateDto>("/api/workflow/template", JsonOptions);
        template.Should().NotBeNull();

        var originalPhases = template!.Phases
            .Select(phase => (object)new
            {
                id = (Guid?)phase.Id,
                name = phase.Name,
                defaultActor = phase.DefaultActor.ToString(),
                orderIndex = phase.OrderIndex,
                color = phase.Color
            })
            .ToList();

        var withDeploy = originalPhases
            .Append(new { id = (Guid?)null, name = "Deploy", defaultActor = "Human", orderIndex = template.Phases.Count, color = "#15803d" })
            .ToList();

        try
        {
            var put = await client.PutAsync("/api/workflow/template", JsonContent(new { phases = withDeploy }));
            put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
            var updated = await put.Content.ReadFromJsonAsync<WorkflowTemplateDto>(JsonOptions);
            updated!.Phases.Should().Contain(phase => phase.Name == "Deploy");

            var prompt = await CreateRootPromptAsync(client, "Tarefa pós-template");
            var workflow = await client.GetFromJsonAsync<WorkflowDto>($"/api/prompts/{prompt.Id}/workflow", JsonOptions);
            workflow!.Phases.Should().Contain(phase => phase.Name == "Deploy");
        }
        finally
        {
            await client.PutAsync("/api/workflow/template", JsonContent(new { phases = originalPhases }));
        }
    }

    [Fact]
    public async Task Workflow_changes_are_broadcast_over_signalr()
    {
        var client = factory.CreateClient();

        var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/prompts"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        var received = new TaskCompletionSource<TaskSummaryDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<TaskSummaryDto>("TaskWorkflowChanged", summary =>
        {
            if (summary.Title == "Tarefa em tempo real")
            {
                received.TrySetResult(summary);
            }
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinTasks");

        try
        {
            var prompt = await CreateRootPromptAsync(client, "Tarefa em tempo real");
            var workflow = await client.GetFromJsonAsync<WorkflowDto>($"/api/prompts/{prompt.Id}/workflow", JsonOptions);
            await client.PostAsync(
                $"/api/prompts/{prompt.Id}/workflow/advance",
                JsonContent(new { rowVersion = workflow!.RowVersion, note = (string?)null }));

            var winner = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            winner.Should().Be(received.Task, "the workflow change should reach the tasks:all SignalR group");
            (await received.Task).PromptId.Should().Be(prompt.Id);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}

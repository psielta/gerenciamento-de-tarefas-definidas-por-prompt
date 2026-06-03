using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Prompts;
using PromptTasks.Infrastructure.Persistence;

namespace PromptTasks.Api.IntegrationTests;

public sealed class ApiFlowTests(PromptTasksApiFactory factory) : IClassFixture<PromptTasksApiFactory>, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"prompttasks-api-{Guid.NewGuid():N}");

    [Fact]
    public async Task Product_flow_persists_versions_references_concurrency_and_signalr_events()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "src", "main.go"), "package main");

        var client = factory.CreateClient();

        var invalidResponse = await client.PostAsJsonAsync(
            "/api/working-directories/validate-path",
            new { absolutePath = Path.Combine(_tempRoot, "missing") },
            JsonOptions);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var invalidPath = await invalidResponse.Content.ReadFromJsonAsync<ValidatePathResponse>(JsonOptions);
        invalidPath!.IsValid.Should().BeFalse();

        var wdResponse = await client.PostAsJsonAsync(
            "/api/working-directories",
            new { name = "repo", absolutePath = _tempRoot, respectGitignore = true },
            JsonOptions);
        wdResponse.StatusCode.Should().Be(HttpStatusCode.Created, await wdResponse.Content.ReadAsStringAsync());
        var wd = await wdResponse.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);
        wd.Should().NotBeNull();

        var search = await client.GetFromJsonAsync<FileSearchResultDto[]>(
            $"/api/files/search?workingDirectoryId={wd!.Id}&query=main&limit=20",
            JsonOptions);
        search.Should().Contain(item => item.RelativePath == "src/main.go");

        await using var hub = CreateHubConnection();
        var createdTcs = new TaskCompletionSource<PromptDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updatedTcs = new TaskCompletionSource<PromptDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deletedTcs = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

        hub.On<PromptDto>("PromptCreated", prompt => createdTcs.TrySetResult(prompt));
        hub.On<PromptDto>("PromptUpdated", prompt => updatedTcs.TrySetResult(prompt));
        hub.On<Guid, Guid>("PromptDeleted", (promptId, _) => deletedTcs.TrySetResult(promptId));

        await hub.StartAsync();
        await hub.InvokeAsync("JoinWorkingDirectory", wd.Id);

        var createResponse = await client.PostAsJsonAsync(
            "/api/prompts",
            new
            {
                workingDirectoryId = wd.Id,
                title = "Inspect main",
                content = "Please inspect @src/main.go",
                targetAgent = TargetAgent.Codex,
                kind = PromptKind.General,
                status = PromptStatus.Draft,
                mentions = new[] { new { id = "src/main.go", label = "src/main.go" } }
            },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<PromptDto>(JsonOptions);
        created.Should().NotBeNull();
        (await createdTcs.Task.WaitAsync(TimeSpan.FromSeconds(10))).Id.Should().Be(created!.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Prompts.Should().ContainSingle(prompt => prompt.Id == created.Id);
            db.PromptVersions.Should().ContainSingle(version =>
                version.PromptId == created.Id && version.VersionNumber == 1);
            db.PromptFileReferences.Should().ContainSingle(reference =>
                reference.PromptId == created.Id && reference.RelativePath == "src/main.go");
        }

        var current = await client.GetFromJsonAsync<PromptDto>($"/api/prompts/{created.Id}", JsonOptions);
        current.Should().NotBeNull();

        var updatePayload = new
        {
            title = "Inspect main updated",
            content = "Please inspect @src/main.go again",
            targetAgent = TargetAgent.Codex,
            kind = PromptKind.General,
            status = PromptStatus.Ready,
            rowVersion = current!.RowVersion,
            mentions = new[] { new { id = "src/main.go", label = "src/main.go" } }
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/prompts/{created.Id}", updatePayload, JsonOptions);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<PromptDto>(JsonOptions);
        updated!.CurrentVersion.Should().Be(2);
        (await updatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10))).Id.Should().Be(created.Id);

        var conflictResponse = await client.PutAsJsonAsync($"/api/prompts/{created.Id}", updatePayload, JsonOptions);
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var versions = await client.GetFromJsonAsync<PromptVersionDto[]>($"/api/prompts/{created.Id}/versions", JsonOptions);
        versions.Should().HaveCount(2);

        var deleteResponse = await client.DeleteAsync($"/api/prompts/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await deletedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(created.Id);
    }

    [Fact]
    public async Task Linked_document_flow_versions_markdown_and_updates_over_signalr()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "repo"));
        var planDirectory = Path.Combine(_tempRoot, "plans");
        Directory.CreateDirectory(planDirectory);
        var planPath = Path.Combine(planDirectory, "claude-plan.md");
        await File.WriteAllTextAsync(planPath, "# Initial plan");

        var client = factory.CreateClient();
        var wdResponse = await client.PostAsJsonAsync(
            "/api/working-directories",
            new { name = "repo", absolutePath = Path.Combine(_tempRoot, "repo"), respectGitignore = true },
            JsonOptions);
        wdResponse.EnsureSuccessStatusCode();
        var wd = await wdResponse.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);

        await using var hub = CreateHubConnection();
        var linkedTcs = new TaskCompletionSource<LinkedDocumentDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updatedTcs = new TaskCompletionSource<LinkedDocumentDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<LinkedDocumentDto>("LinkedDocumentLinked", document => linkedTcs.TrySetResult(document));
        hub.On<LinkedDocumentDto>("LinkedDocumentUpdated", document =>
        {
            if (document.CurrentVersion >= 2)
            {
                updatedTcs.TrySetResult(document);
            }
        });

        await hub.StartAsync();
        await hub.InvokeAsync("JoinWorkingDirectory", wd!.Id);

        var createResponse = await client.PostAsJsonAsync(
            "/api/prompts",
            new
            {
                workingDirectoryId = wd.Id,
                title = "Plan prompt",
                content = "Create a plan",
                targetAgent = TargetAgent.ClaudeCode,
                kind = PromptKind.Planning,
                status = PromptStatus.Draft,
                mentions = Array.Empty<object>()
            },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var prompt = await createResponse.Content.ReadFromJsonAsync<PromptDto>(JsonOptions);

        var linkResponse = await client.PostAsJsonAsync(
            $"/api/prompts/{prompt!.Id}/linked-documents",
            new { absolutePath = planPath, documentType = LinkedDocumentType.ClaudeCodePlan },
            JsonOptions);
        linkResponse.EnsureSuccessStatusCode();
        var linked = await linkResponse.Content.ReadFromJsonAsync<LinkedDocumentDto>(JsonOptions);
        linked!.CurrentVersion.Should().Be(1);
        (await linkedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10))).Id.Should().Be(linked.Id);

        var content = await client.GetFromJsonAsync<LinkedDocumentContentDto>(
            $"/api/linked-documents/{linked.Id}/content",
            JsonOptions);
        content!.Content.Should().Be("# Initial plan");

        await File.WriteAllTextAsync(planPath, "# Updated plan");
        var updated = await updatedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        updated.Id.Should().Be(linked.Id);
        updated.CurrentVersion.Should().Be(2);

        var versions = await client.GetFromJsonAsync<LinkedDocumentVersionDto[]>(
            $"/api/linked-documents/{linked.Id}/versions",
            JsonOptions);
        versions.Should().HaveCount(2);

        var updatedContent = await client.GetFromJsonAsync<LinkedDocumentContentDto>(
            $"/api/linked-documents/{linked.Id}/content",
            JsonOptions);
        updatedContent!.Content.Should().Be("# Updated plan");

        var deleteResponse = await client.DeleteAsync($"/api/linked-documents/{linked.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Prompt_template_flow_renders_draft_and_persists_prompt()
    {
        Directory.CreateDirectory(Path.Combine(_tempRoot, "repo"));
        var planDirectory = Path.Combine(_tempRoot, "plans");
        Directory.CreateDirectory(planDirectory);
        var planPath = Path.Combine(planDirectory, "review-plan.md");
        await File.WriteAllTextAsync(planPath, "# Review plan");

        var client = factory.CreateClient();
        var templates = await client.GetFromJsonAsync<PromptTemplateDto[]>("/api/prompt-templates", JsonOptions);
        templates.Should().NotBeNull();
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ReviewPlan &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.Planning);
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ImplementPlan &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.General);
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ReviewPlanWithParentPrompt &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.Planning);
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ReReviewPlan &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.Planning);
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ImplementPlanInWorktree &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.General);
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ReviewPullRequest &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.General &&
            template.Input != null &&
            template.Input.Key == "pullRequest");
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.ReReviewPullRequest &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.General &&
            template.Input != null &&
            template.Input.Key == "pullRequest" &&
            template.Inputs.Count == 2 &&
            template.Inputs.Any(input => input.Key == "reviewNotes" && input.Multiline));
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.MergePullRequest &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.General &&
            template.Input != null &&
            template.Input.Key == "pullRequest");
        templates.Should().Contain(template =>
            template.Key == PromptTemplateKey.RebaseCurrentBranch &&
            template.DefaultTargetAgent == TargetAgent.Codex &&
            template.DefaultKind == PromptKind.General &&
            template.Input == null);

        var wdResponse = await client.PostAsJsonAsync(
            "/api/working-directories",
            new { name = "repo", absolutePath = Path.Combine(_tempRoot, "repo"), respectGitignore = true },
            JsonOptions);
        wdResponse.EnsureSuccessStatusCode();
        var wd = await wdResponse.Content.ReadFromJsonAsync<WorkingDirectoryDto>(JsonOptions);

        var createResponse = await client.PostAsJsonAsync(
            "/api/prompts",
            new
            {
                workingDirectoryId = wd!.Id,
                title = "Plan prompt",
                content = "Create a plan",
                targetAgent = TargetAgent.ClaudeCode,
                kind = PromptKind.Planning,
                status = PromptStatus.Draft,
                mentions = Array.Empty<object>()
            },
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var prompt = await createResponse.Content.ReadFromJsonAsync<PromptDto>(JsonOptions);

        var linkResponse = await client.PostAsJsonAsync(
            $"/api/prompts/{prompt!.Id}/linked-documents",
            new { absolutePath = planPath, documentType = LinkedDocumentType.ClaudeCodePlan },
            JsonOptions);
        linkResponse.EnsureSuccessStatusCode();
        var linked = await linkResponse.Content.ReadFromJsonAsync<LinkedDocumentDto>(JsonOptions);

        var draftResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked!.Id}/prompt-drafts",
            new { templateKey = PromptTemplateKey.ReviewPlan },
            JsonOptions);
        draftResponse.EnsureSuccessStatusCode();
        var draft = await draftResponse.Content.ReadFromJsonAsync<GeneratedPromptDraftDto>(JsonOptions);
        draft.Should().NotBeNull();
        draft!.LinkedDocumentId.Should().Be(linked.Id);
        draft.WorkingDirectoryId.Should().Be(wd.Id);
        draft.ParentPromptId.Should().Be(prompt.Id);
        draft.TargetAgent.Should().Be(TargetAgent.Codex);
        draft.Kind.Should().Be(PromptKind.Planning);
        draft.Content.Should().Be($"Given the plan \"{planPath}\", validate the plan, approve it, or point out improvements.");

        var prDraftResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new { templateKey = PromptTemplateKey.ReviewPullRequest, pullRequest = "42" },
            JsonOptions);
        prDraftResponse.EnsureSuccessStatusCode();
        var prDraft = await prDraftResponse.Content.ReadFromJsonAsync<GeneratedPromptDraftDto>(JsonOptions);
        prDraft!.Title.Should().Be("Review PR #42: review-plan.md");
        prDraft.Content.Should().Contain($"Review the PR #42 that implements the plan `{planPath}`.");

        var reReviewDraftResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new
            {
                templateKey = PromptTemplateKey.ReReviewPullRequest,
                inputs = new Dictionary<string, string>
                {
                    ["pullRequest"] = "42",
                    ["reviewNotes"] = "High: missing integration test."
                }
            },
            JsonOptions);
        reReviewDraftResponse.EnsureSuccessStatusCode();
        var reReviewDraft = await reReviewDraftResponse.Content.ReadFromJsonAsync<GeneratedPromptDraftDto>(JsonOptions);
        reReviewDraft!.Title.Should().Be("Re-review PR #42: review-plan.md");
        reReviewDraft.Content.Should().StartWith("/review");
        reReviewDraft.Content.Should().Contain($"The PR implements the plan `{planPath}`.");
        reReviewDraft.Content.Should().Contain("High: missing integration test.");

        var mergeDraftResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new { templateKey = PromptTemplateKey.MergePullRequest, pullRequest = "42" },
            JsonOptions);
        mergeDraftResponse.EnsureSuccessStatusCode();
        var mergeDraft = await mergeDraftResponse.Content.ReadFromJsonAsync<GeneratedPromptDraftDto>(JsonOptions);
        mergeDraft!.Title.Should().Be("Merge PR #42: review-plan.md");
        mergeDraft.Content.Should().Contain($"Merge the PR #42 that implements the plan `{planPath}`.");

        var rebaseDraftResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new { templateKey = PromptTemplateKey.RebaseCurrentBranch },
            JsonOptions);
        rebaseDraftResponse.EnsureSuccessStatusCode();
        var rebaseDraft = await rebaseDraftResponse.Content.ReadFromJsonAsync<GeneratedPromptDraftDto>(JsonOptions);
        rebaseDraft!.Title.Should().Be("Update branch from main: review-plan.md");
        rebaseDraft.Content.Should().Contain("Update my current branch/worktree with the latest changes from the remote main branch using rebase.");
        rebaseDraft.Content.Should().Contain("If there are conflicts, stop and tell me so we can resolve them together.");

        var generatedPromptResponse = await client.PostAsJsonAsync(
            "/api/prompts",
            new
            {
                workingDirectoryId = draft.WorkingDirectoryId,
                parentPromptId = draft.ParentPromptId,
                title = draft.Title,
                content = draft.Content,
                targetAgent = draft.TargetAgent,
                kind = draft.Kind,
                status = PromptStatus.Draft,
                mentions = Array.Empty<object>()
            },
            JsonOptions);
        generatedPromptResponse.EnsureSuccessStatusCode();
        var generatedPrompt = await generatedPromptResponse.Content.ReadFromJsonAsync<PromptDto>(JsonOptions);
        generatedPrompt!.Content.Should().Be(draft.Content);
        generatedPrompt.ParentPromptId.Should().Be(prompt.Id);

        var rootPrompts = await client.GetFromJsonAsync<PromptDto[]>(
            $"/api/prompts?workingDirectoryId={wd.Id}&rootOnly=true",
            JsonOptions);
        rootPrompts.Should().ContainSingle(item => item.Id == prompt.Id);
        rootPrompts.Should().NotContain(item => item.Id == generatedPrompt.Id);

        var childPrompts = await client.GetFromJsonAsync<PromptDto[]>(
            $"/api/prompts?workingDirectoryId={wd.Id}&parentPromptId={prompt.Id}",
            JsonOptions);
        childPrompts.Should().ContainSingle(item => item.Id == generatedPrompt.Id);

        var invalidTemplateResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new { templateKey = 999 },
            JsonOptions);
        invalidTemplateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missingPrResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new { templateKey = PromptTemplateKey.ReviewPullRequest },
            JsonOptions);
        missingPrResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missingReReviewNotesResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new
            {
                templateKey = PromptTemplateKey.ReReviewPullRequest,
                inputs = new Dictionary<string, string> { ["pullRequest"] = "42" }
            },
            JsonOptions);
        missingReReviewNotesResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missingMergePrResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{linked.Id}/prompt-drafts",
            new { templateKey = PromptTemplateKey.MergePullRequest },
            JsonOptions);
        missingMergePrResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var missingDocumentResponse = await client.PostAsJsonAsync(
            $"/api/linked-documents/{Guid.CreateVersion7()}/prompt-drafts",
            new { templateKey = PromptTemplateKey.ReviewPlan },
            JsonOptions);
        missingDocumentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private HubConnection CreateHubConnection()
    {
        factory.CreateClient();

        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/prompts"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .WithAutomaticReconnect()
            .Build();
    }
}

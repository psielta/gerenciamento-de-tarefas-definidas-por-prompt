using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.DeleteChatSession;
using PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;
using PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;
using PromptTasks.Application.Features.Ai.Commands.RefinePrompt;
using PromptTasks.Application.Features.Ai.Commands.SendChatMessage;
using PromptTasks.Application.Features.Ai.Commands.StartChatSession;
using PromptTasks.Application.Features.Ai.Commands.TranslatePrompt;
using PromptTasks.Application.Features.Ai.Commands.UpdateAiSettings;
using PromptTasks.Application.Features.Ai.Models;
using PromptTasks.Application.Features.Ai.Queries.GetAiModels;
using PromptTasks.Application.Features.Ai.Queries.GetAiSettings;
using PromptTasks.Application.Features.Ai.Queries.GetChatSession;
using PromptTasks.Application.Features.Ai.Queries.ListChatSessions;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController(ISender sender) : ControllerBase
{
    [HttpGet("models")]
    public async Task<ActionResult<IReadOnlyList<GeminiModelDto>>> GetModels(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAiModelsQuery(), cancellationToken));

    [HttpGet("settings")]
    public async Task<ActionResult<AiSettingsDto>> GetSettings(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAiSettingsQuery(), cancellationToken));

    [HttpPut("settings")]
    public async Task<ActionResult<AiSettingsDto>> UpdateSettings(
        UpdateAiSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateAiSettingsCommand(
            request.Model,
            request.Temperature,
            request.ThinkingEnabled,
            request.ThinkingBudget,
            request.ThinkingLevel),
            cancellationToken));

    [HttpPost("refine")]
    public async Task<ActionResult<RefinedPromptDto>> Refine(
        RefineRequest request,
        CancellationToken cancellationToken)
    {
        var thinking = new GeminiThinking(
            request.ThinkingMode ?? "none",
            request.ThinkingBudget,
            request.ThinkingLevel);

        return Ok(await sender.Send(
            new RefinePromptCommand(
                request.Content,
                request.Model,
                request.Temperature,
                thinking,
                request.WorkingDirectoryId,
                request.ContextFiles ?? Array.Empty<string>(),
                request.CustomInstructions),
            cancellationToken));
    }

    [HttpPost("translate")]
    public async Task<ActionResult<RefinedPromptDto>> Translate(
        TranslateRequest request,
        CancellationToken cancellationToken)
    {
        var thinking = new GeminiThinking(
            request.ThinkingMode ?? "none",
            request.ThinkingBudget,
            request.ThinkingLevel);

        return Ok(await sender.Send(
            new TranslatePromptCommand(
                request.Content,
                request.Model,
                request.Temperature,
                thinking),
            cancellationToken));
    }

    [HttpPost("notes/generate")]
    public async Task<ActionResult<GeneratedNoteDto>> GenerateNote(
        GenerateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var thinking = new GeminiThinking(
            request.ThinkingMode ?? "none",
            request.ThinkingBudget,
            request.ThinkingLevel);

        return Ok(await sender.Send(
            new GenerateNoteMarkdownCommand(
                request.Instruction,
                request.Format,
                request.Model,
                request.Temperature,
                thinking,
                request.NotebookId,
                request.CurrentContent),
            cancellationToken));
    }

    [HttpPost("diagrams/mermaid/generate")]
    public async Task<ActionResult<GeneratedMermaidDto>> GenerateMermaid(
        GenerateMermaidRequest request,
        CancellationToken cancellationToken)
    {
        var thinking = new GeminiThinking(
            request.ThinkingMode ?? "none",
            request.ThinkingBudget,
            request.ThinkingLevel);

        return Ok(await sender.Send(
            new GenerateMermaidDiagramCommand(
                request.Instruction,
                request.DiagramKind,
                request.Model,
                request.Temperature,
                thinking,
                request.WorkingDirectoryId,
                request.DiagramId,
                request.CurrentCode),
            cancellationToken));
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AiChatSessionDto>>> ListSessions(
        [FromQuery] Guid? workingDirectoryId,
        [FromQuery] Guid? promptId,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ListChatSessionsQuery(workingDirectoryId, promptId), cancellationToken));

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<AiChatSessionDto>> GetSession(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetChatSessionQuery(id), cancellationToken));

    [HttpPost("sessions")]
    public async Task<ActionResult<AiChatSessionDto>> StartSession(
        StartSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartChatSessionCommand(
            request.Title,
            request.WorkingDirectoryId,
            request.PromptId,
            request.Model,
            request.Temperature,
            request.ThinkingEnabled,
            request.ThinkingBudget,
            request.ThinkingLevel),
            cancellationToken);

        return CreatedAtAction(nameof(GetSession), new { id = result.Id }, result);
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteChatSessionCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");

        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
        };

        var stream = sender.CreateStream(new SendChatMessageCommand(
            id,
            request.Message,
            request.IncludePromptContext,
            request.PromptContent),
            cancellationToken);

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(chunk, settings);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    public sealed record UpdateAiSettingsRequest(
        string Model,
        double Temperature,
        bool ThinkingEnabled,
        int? ThinkingBudget,
        string? ThinkingLevel);

    public sealed record RefineRequest(
        string Content,
        string Model,
        double Temperature,
        string? ThinkingMode,
        int? ThinkingBudget,
        string? ThinkingLevel,
        Guid? WorkingDirectoryId,
        IReadOnlyList<string>? ContextFiles,
        string? CustomInstructions);

    public sealed record TranslateRequest(
        string Content,
        string Model,
        double Temperature,
        string? ThinkingMode,
        int? ThinkingBudget,
        string? ThinkingLevel);

    public sealed record GenerateNoteRequest(
        string Instruction,
        string? Format,
        string Model,
        double Temperature,
        string? ThinkingMode,
        int? ThinkingBudget,
        string? ThinkingLevel,
        Guid? NotebookId,
        string? CurrentContent);

    public sealed record GenerateMermaidRequest(
        string Instruction,
        string? DiagramKind,
        string Model,
        double Temperature,
        string? ThinkingMode,
        int? ThinkingBudget,
        string? ThinkingLevel,
        Guid? WorkingDirectoryId,
        Guid? DiagramId,
        string? CurrentCode);

    public sealed record StartSessionRequest(
        string? Title,
        Guid? WorkingDirectoryId,
        Guid? PromptId,
        string Model,
        double Temperature,
        bool ThinkingEnabled,
        int? ThinkingBudget,
        string? ThinkingLevel);

    public sealed record SendMessageRequest(
        string Message,
        bool IncludePromptContext,
        string? PromptContent);
}

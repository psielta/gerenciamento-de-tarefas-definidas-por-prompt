using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Prompts.Commands.CreatePrompt;
using PromptTasks.Application.Features.Prompts.Commands.DeletePrompt;
using PromptTasks.Application.Features.Prompts.Commands.UpdatePrompt;
using PromptTasks.Application.Features.Prompts.Commands.UpdatePromptStatus;
using PromptTasks.Application.Features.Prompts.Queries.GetPrompt;
using PromptTasks.Application.Features.Prompts.Queries.GetPromptByTaskNumber;
using PromptTasks.Application.Features.Prompts.Queries.GetPrompts;
using PromptTasks.Application.Features.Prompts.Queries.GetPromptVersions;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api/prompts")]
public sealed class PromptsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromptDto>>> Get(
        [FromQuery] Guid? workingDirectoryId,
        [FromQuery] Guid? parentPromptId,
        [FromQuery] bool rootOnly,
        [FromQuery] PromptStatus? status,
        [FromQuery] TargetAgent? agent,
        [FromQuery] PromptKind? kind,
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPromptsQuery(workingDirectoryId, parentPromptId, rootOnly, status, agent, kind, q), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PromptDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPromptQuery(id), cancellationToken));

    [HttpGet("by-task-number")]
    public async Task<ActionResult<PromptDto>> GetByTaskNumber(
        [FromQuery] Guid workingDirectoryId,
        [FromQuery] string taskNumber,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPromptByTaskNumberQuery(workingDirectoryId, taskNumber), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PromptDto>> Create(CreatePromptRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreatePromptCommand(
                request.WorkingDirectoryId,
                request.ParentPromptId,
                request.Title,
                request.Content,
                request.TargetAgent,
                request.Kind,
                request.Status,
                request.SourceTemplateKey,
                request.Mentions),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PromptDto>> Update(
        Guid id,
        UpdatePromptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdatePromptCommand(
                id,
                request.Title,
                request.Content,
                request.TargetAgent,
                request.Kind,
                request.Status,
                request.RowVersion,
                request.Mentions),
            cancellationToken);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<PromptDto>> UpdateStatus(
        Guid id,
        UpdatePromptStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdatePromptStatusCommand(id, request.Status, request.RowVersion), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeletePromptCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<PromptVersionDto>>> GetVersions(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPromptVersionsQuery(id), cancellationToken));

    public sealed record CreatePromptRequest(
        Guid WorkingDirectoryId,
        Guid? ParentPromptId,
        string Title,
        string Content,
        TargetAgent TargetAgent,
        PromptKind Kind,
        PromptStatus Status,
        PromptTemplateKey? SourceTemplateKey,
        IReadOnlyList<FileMentionDto>? Mentions);

    public sealed record UpdatePromptRequest(
        string Title,
        string Content,
        TargetAgent TargetAgent,
        PromptKind Kind,
        PromptStatus Status,
        string RowVersion,
        IReadOnlyList<FileMentionDto>? Mentions);

    public sealed record UpdatePromptStatusRequest(PromptStatus Status, string RowVersion);
}

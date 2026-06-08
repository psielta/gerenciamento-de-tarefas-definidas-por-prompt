using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Diagrams.Commands.CreateDiagram;
using PromptTasks.Application.Features.Diagrams.Commands.DeleteDiagram;
using PromptTasks.Application.Features.Diagrams.Commands.SetDiagramArchived;
using PromptTasks.Application.Features.Diagrams.Commands.UpdateDiagram;
using PromptTasks.Application.Features.Diagrams.Queries.GetDiagram;
using PromptTasks.Application.Features.Diagrams.Queries.GetDiagrams;
using PromptTasks.Domain.Diagrams;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api/diagrams")]
public sealed class DiagramsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiagramSummaryDto>>> Get(
        [FromQuery] Guid? workingDirectoryId,
        [FromQuery] string? q,
        [FromQuery] DiagramType? type,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetDiagramsQuery(workingDirectoryId, q, type, includeArchived), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DiagramDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetDiagramQuery(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<DiagramDto>> Create(CreateDiagramRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateDiagramCommand(
                request.WorkingDirectoryId,
                request.Title,
                request.Type,
                request.Description,
                request.Content,
                request.MetadataJson),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DiagramDto>> Update(
        Guid id,
        UpdateDiagramRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateDiagramCommand(id, request.Title, request.Content, request.Description, request.MetadataJson),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<DiagramDto>> SetArchived(
        Guid id,
        SetArchivedRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SetDiagramArchivedCommand(id, request.IsArchived), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteDiagramCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record CreateDiagramRequest(
        Guid WorkingDirectoryId,
        string Title,
        DiagramType Type,
        string? Description,
        string? Content,
        string? MetadataJson);

    public sealed record UpdateDiagramRequest(
        string Title,
        string Content,
        string? Description,
        string? MetadataJson);

    public sealed record SetArchivedRequest(bool IsArchived);
}

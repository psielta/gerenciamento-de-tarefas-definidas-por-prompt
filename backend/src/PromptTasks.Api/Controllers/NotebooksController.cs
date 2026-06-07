using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Notebooks.Commands.CreateNotebook;
using PromptTasks.Application.Features.Notebooks.Commands.DeleteNotebook;
using PromptTasks.Application.Features.Notebooks.Commands.SetNotebookArchived;
using PromptTasks.Application.Features.Notebooks.Commands.UpdateNotebook;
using PromptTasks.Application.Features.Notebooks.Queries.GetNotebook;
using PromptTasks.Application.Features.Notebooks.Queries.GetNotebooks;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api/notebooks")]
public sealed class NotebooksController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotebookDto>>> Get(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNotebooksQuery(includeArchived), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotebookDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNotebookQuery(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<NotebookDto>> Create(
        CreateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateNotebookCommand(request.Title, request.Description, request.WorkingDirectoryId),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotebookDto>> Update(
        Guid id,
        UpdateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateNotebookCommand(id, request.Title, request.Description, request.WorkingDirectoryId),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<NotebookDto>> SetArchived(
        Guid id,
        SetArchivedRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SetNotebookArchivedCommand(id, request.IsArchived), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteNotebookCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record CreateNotebookRequest(string Title, string? Description, Guid? WorkingDirectoryId);

    public sealed record UpdateNotebookRequest(string Title, string? Description, Guid? WorkingDirectoryId);

    public sealed record SetArchivedRequest(bool IsArchived);
}

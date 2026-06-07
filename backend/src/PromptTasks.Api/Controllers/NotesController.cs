using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Notes.Commands.CreateNote;
using PromptTasks.Application.Features.Notes.Commands.DeleteNote;
using PromptTasks.Application.Features.Notes.Commands.SetNoteArchived;
using PromptTasks.Application.Features.Notes.Commands.SetNotePinned;
using PromptTasks.Application.Features.Notes.Commands.UpdateNote;
using PromptTasks.Application.Features.Notes.Queries.GetNote;
using PromptTasks.Application.Features.Notes.Queries.GetNotes;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api/notes")]
public sealed class NotesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> Get(
        [FromQuery] Guid? notebookId,
        [FromQuery] string? q,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNotesQuery(notebookId, q, includeArchived), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNoteQuery(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateNoteCommand(request.NotebookId, request.Title, request.ContentMarkdown),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteDto>> Update(
        Guid id,
        UpdateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateNoteCommand(id, request.Title, request.ContentMarkdown),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/pin")]
    public async Task<ActionResult<NoteDto>> SetPinned(
        Guid id,
        SetPinnedRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SetNotePinnedCommand(id, request.IsPinned), cancellationToken));

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<NoteDto>> SetArchived(
        Guid id,
        SetArchivedRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SetNoteArchivedCommand(id, request.IsArchived), cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteNoteCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record CreateNoteRequest(Guid NotebookId, string Title, string? ContentMarkdown);

    public sealed record UpdateNoteRequest(string Title, string ContentMarkdown);

    public sealed record SetPinnedRequest(bool IsPinned);

    public sealed record SetArchivedRequest(bool IsArchived);
}

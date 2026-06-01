using MediatR;
using Microsoft.AspNetCore.Mvc;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.WorkingDirectories.Commands.CreateWorkingDirectory;
using PromptTasks.Application.Features.WorkingDirectories.Commands.DeleteWorkingDirectory;
using PromptTasks.Application.Features.WorkingDirectories.Commands.UpdateWorkingDirectory;
using PromptTasks.Application.Features.WorkingDirectories.Queries.GetWorkingDirectories;
using PromptTasks.Application.Features.WorkingDirectories.Queries.GetWorkingDirectory;
using PromptTasks.Application.Features.WorkingDirectories.Queries.ValidateWorkingDirectoryPath;

namespace PromptTasks.Api.Controllers;

[ApiController]
[Route("api/working-directories")]
public sealed class WorkingDirectoriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkingDirectoryDto>>> Get(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWorkingDirectoriesQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkingDirectoryDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetWorkingDirectoryQuery(id), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<WorkingDirectoryDto>> Create(
        CreateWorkingDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateWorkingDirectoryCommand(
                request.Name,
                request.AbsolutePath,
                request.RespectGitignore,
                request.EnableAiContext,
                request.TaskNumberPattern),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkingDirectoryDto>> Update(
        Guid id,
        UpdateWorkingDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateWorkingDirectoryCommand(
                id,
                request.Name,
                request.AbsolutePath,
                request.RespectGitignore,
                request.EnableAiContext,
                request.TaskNumberPattern),
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteWorkingDirectoryCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("validate-path")]
    public async Task<ActionResult<ValidatePathResponse>> ValidatePath(
        ValidatePathRequest request,
        CancellationToken cancellationToken) =>
        Ok(await sender.Send(new ValidateWorkingDirectoryPathQuery(request.AbsolutePath), cancellationToken));

    public sealed record CreateWorkingDirectoryRequest(
        string Name,
        string AbsolutePath,
        bool RespectGitignore = true,
        bool EnableAiContext = false,
        string? TaskNumberPattern = null);
    public sealed record UpdateWorkingDirectoryRequest(
        string Name,
        string AbsolutePath,
        bool RespectGitignore,
        bool EnableAiContext,
        string? TaskNumberPattern = null);
    public sealed record ValidatePathRequest(string AbsolutePath);
}

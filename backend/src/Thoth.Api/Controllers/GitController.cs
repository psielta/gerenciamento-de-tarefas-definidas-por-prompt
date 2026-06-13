using MediatR;
using Microsoft.AspNetCore.Mvc;
using Thoth.Application.Common.Models;
using Thoth.Application.Features.Git.Queries.GetFileGitContent;
using Thoth.Application.Features.Git.Queries.GetFileGitHistory;
using Thoth.Application.Features.Git.Queries.GetGitDiff;
using Thoth.Application.Features.Git.Queries.GetGitStatus;
using Thoth.Application.Features.Git.Queries.GetOriginalFileContent;

namespace Thoth.Api.Controllers;

[ApiController]
[Route("api/git")]
public sealed class GitController(ISender sender) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<IReadOnlyList<GitFileStatusDto>>> GetStatus(
        [FromQuery] Guid workingDirectoryId,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetGitStatusQuery(workingDirectoryId), cancellationToken));

    [HttpGet("original-file")]
    public async Task<ActionResult<GitOriginalFileDto>> GetOriginalFile(
        [FromQuery] Guid workingDirectoryId,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetOriginalFileContentQuery(workingDirectoryId, path), cancellationToken));

    [HttpGet("diff")]
    public async Task<ActionResult<GitDiffDto>> GetDiff(
        [FromQuery] Guid workingDirectoryId,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetGitDiffQuery(workingDirectoryId, path), cancellationToken));

    [HttpGet("history")]
    public async Task<ActionResult<GitFileHistoryDto>> GetHistory(
        [FromQuery] Guid workingDirectoryId,
        [FromQuery] string path,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetFileGitHistoryQuery(workingDirectoryId, path), cancellationToken));

    [HttpGet("file-content")]
    public async Task<ActionResult<GitFileContentAtCommitDto>> GetFileContent(
        [FromQuery] Guid workingDirectoryId,
        [FromQuery] string path,
        [FromQuery] string hash,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetFileGitContentQuery(workingDirectoryId, path, hash), cancellationToken));
}

using MediatR;
using Microsoft.Extensions.Logging;
using Thoth.Application.Common.Exceptions;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;

namespace Thoth.Application.Features.Git.Queries.GetFileGitContent;

public sealed class GetFileGitContentHandler(
    IApplicationDbContext context,
    IGitCommandRunner git,
    ICurrentUser currentUser,
    ILogger<GetFileGitContentHandler> logger)
    : IRequestHandler<GetFileGitContentQuery, GitFileContentAtCommitDto>
{
    private const long MaxContentBytes = 1_000_000;

    public async Task<GitFileContentAtCommitDto> Handle(
        GetFileGitContentQuery request,
        CancellationToken cancellationToken)
    {
        var directory = context.WorkingDirectories
            .FirstOrDefault(item => item.Id == request.WorkingDirectoryId && item.OwnerId == currentUser.UserId);

        if (directory is null)
        {
            throw new NotFoundException("Working directory was not found.");
        }

        var normalizedPath = GitRelativePath.Normalize(request.Path);
        var objectSpec = $"{request.Hash}:./{normalizedPath}";

        var sizeResult = await git.RunAsync(
            directory.AbsolutePath,
            ["cat-file", "-s", objectSpec],
            cancellationToken);

        if (sizeResult.ExitCode != 0)
        {
            logger.LogDebug(
                "Git content was unavailable for {Path} at {Hash} in {WorkingDirectory}. Exit code: {ExitCode}; stderr: {StandardError}",
                normalizedPath,
                request.Hash,
                directory.AbsolutePath,
                sizeResult.ExitCode,
                sizeResult.StandardError);
            return new GitFileContentAtCommitDto(string.Empty, false, false, false);
        }

        if (!long.TryParse(sizeResult.StandardOutput.Trim(), out var sizeBytes))
        {
            return new GitFileContentAtCommitDto(string.Empty, true, false, false);
        }

        if (sizeBytes > MaxContentBytes)
        {
            return new GitFileContentAtCommitDto(string.Empty, true, false, true);
        }

        var showResult = await git.RunAsync(
            directory.AbsolutePath,
            ["show", objectSpec],
            cancellationToken);

        if (showResult.ExitCode != 0)
        {
            logger.LogDebug(
                "Git show failed for {Path} at {Hash} in {WorkingDirectory}. Exit code: {ExitCode}; stderr: {StandardError}",
                normalizedPath,
                request.Hash,
                directory.AbsolutePath,
                showResult.ExitCode,
                showResult.StandardError);
            return new GitFileContentAtCommitDto(string.Empty, false, false, false);
        }

        if (showResult.StandardOutput.Contains('\0'))
        {
            return new GitFileContentAtCommitDto(string.Empty, true, true, false);
        }

        return new GitFileContentAtCommitDto(showResult.StandardOutput, true, false, false);
    }
}
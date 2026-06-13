using MediatR;
using Microsoft.Extensions.Logging;
using Thoth.Application.Common.Exceptions;
using Thoth.Application.Common.Interfaces;
using Thoth.Application.Common.Models;

namespace Thoth.Application.Features.Git.Queries.GetFileGitHistory;

public sealed class GetFileGitHistoryHandler(
    IApplicationDbContext context,
    IGitCommandRunner git,
    ICurrentUser currentUser,
    ILogger<GetFileGitHistoryHandler> logger)
    : IRequestHandler<GetFileGitHistoryQuery, GitFileHistoryDto>
{
    public async Task<GitFileHistoryDto> Handle(
        GetFileGitHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var directory = context.WorkingDirectories
            .FirstOrDefault(item => item.Id == request.WorkingDirectoryId && item.OwnerId == currentUser.UserId);

        if (directory is null)
        {
            throw new NotFoundException("Working directory was not found.");
        }

        var probe = await GitRepository.ProbeAsync(git, directory.AbsolutePath, logger, cancellationToken);
        if (!probe.IsRepository)
        {
            return new GitFileHistoryDto(false, []);
        }

        var normalizedPath = GitRelativePath.Normalize(request.Path);
        var result = await git.RunAsync(
            directory.AbsolutePath,
            [
                "-c",
                "core.quotepath=false",
                "log",
                "--no-color",
                "-z",
                "--format=%H%x1f%h%x1f%an%x1f%aI%x1f%P%x1f%s",
                "-n",
                "100",
                "--",
                normalizedPath
            ],
            cancellationToken);

        if (result.ExitCode != 0)
        {
            logger.LogDebug(
                "Git log failed for {Path} in {WorkingDirectory}. Exit code: {ExitCode}; stderr: {StandardError}",
                normalizedPath,
                directory.AbsolutePath,
                result.ExitCode,
                result.StandardError);
            return new GitFileHistoryDto(true, []);
        }

        return new GitFileHistoryDto(true, GitLogParser.Parse(result.StandardOutput));
    }
}
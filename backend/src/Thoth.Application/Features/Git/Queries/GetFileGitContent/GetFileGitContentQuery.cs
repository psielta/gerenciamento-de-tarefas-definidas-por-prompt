using MediatR;
using Thoth.Application.Common.Models;

namespace Thoth.Application.Features.Git.Queries.GetFileGitContent;

public sealed record GetFileGitContentQuery(Guid WorkingDirectoryId, string Path, string Hash)
    : IRequest<GitFileContentAtCommitDto>;
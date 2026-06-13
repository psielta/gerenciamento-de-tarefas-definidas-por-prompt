using MediatR;
using Thoth.Application.Common.Models;

namespace Thoth.Application.Features.Git.Queries.GetFileGitHistory;

public sealed record GetFileGitHistoryQuery(Guid WorkingDirectoryId, string Path)
    : IRequest<GitFileHistoryDto>;
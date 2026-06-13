namespace Thoth.Application.Common.Models;

public sealed record GitFileHistoryDto(bool IsRepository, IReadOnlyList<GitCommitDto> Commits);
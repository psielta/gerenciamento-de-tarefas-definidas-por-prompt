namespace Thoth.Application.Common.Models;

public sealed record GitCommitDto(
    string Hash,
    string ShortHash,
    string Author,
    string Date,
    string Message,
    string ParentHash);
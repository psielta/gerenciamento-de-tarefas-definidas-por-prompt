namespace Thoth.Application.Common.Models;

public sealed record GitFileContentAtCommitDto(
    string Content,
    bool Exists,
    bool IsBinary,
    bool Truncated);
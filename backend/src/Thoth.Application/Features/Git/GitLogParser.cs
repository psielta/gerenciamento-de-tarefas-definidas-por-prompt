using Thoth.Application.Common.Models;

namespace Thoth.Application.Features.Git;

public static class GitLogParser
{
    public static IReadOnlyList<GitCommitDto> Parse(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return Array.Empty<GitCommitDto>();
        }

        var entries = new List<GitCommitDto>();
        var records = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        foreach (var record in records)
        {
            var fields = record.Split('\x1f');
            if (fields.Length < 6)
            {
                continue;
            }

            var parentField = fields[4].Trim();
            var parentHash = string.IsNullOrEmpty(parentField)
                ? string.Empty
                : parentField.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

            entries.Add(new GitCommitDto(
                fields[0],
                fields[1],
                fields[2],
                fields[3],
                fields[5],
                parentHash));
        }

        return entries;
    }
}
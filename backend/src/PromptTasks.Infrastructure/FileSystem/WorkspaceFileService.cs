using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileSystemGlobbing;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Infrastructure.FileSystem;

public sealed class WorkspaceFileService(IMemoryCache cache, IDateTimeProvider dateTimeProvider) : IWorkspaceFileService
{
    private sealed record FileIndexEntry(string RelativePath, string FileName, bool IsDirectory);

    private static readonly string[] ContextFileNames = ["README.md", "CLAUDE.md", "AGENT.md"];
    private const long MaxContextFileBytes = 64 * 1024;
    private const int MaxTotalContextChars = 48_000;

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        ".git",
        ".hg",
        ".svn",
        "bin",
        "obj",
        ".vs",
        ".idea",
        "dist",
        "build",
        ".next",
        ".venv",
        "__pycache__",
        "target",
        ".gradle",
        "artifacts",
        "packages",
        "coverage",
        "TestResults",
        "log",
        "logs",
        ".cache",
        "tmp",
        "temp"
    };

    public Task<ValidatedPathResult> ValidatePathAsync(string absolutePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return Task.FromResult(ValidatedPathResult.Invalid("Path is required."));
        }

        try
        {
            if (!Path.IsPathRooted(absolutePath))
            {
                return Task.FromResult(ValidatedPathResult.Invalid("Path must be absolute."));
            }

            if (!Directory.Exists(absolutePath))
            {
                return Task.FromResult(ValidatedPathResult.Invalid("Path must point to an existing directory."));
            }

            var canonical = CanonicalizeExistingPath(absolutePath);
            if (!Directory.Exists(canonical))
            {
                return Task.FromResult(ValidatedPathResult.Invalid("Path must point to a directory."));
            }

            return Task.FromResult(ValidatedPathResult.Valid(canonical));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Task.FromResult(ValidatedPathResult.Invalid(exception.Message));
        }
    }

    public async Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken)
    {
        string rootCanonical;
        try
        {
            rootCanonical = CanonicalizeExistingPath(rootAbsolutePath);
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException
                                             or PathTraversalException)
        {
            return null;
        }

        var sections = new List<string>();
        var totalChars = 0;

        foreach (var fileName in ContextFileNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                var candidateLogical = Path.GetFullPath(Path.Combine(rootCanonical, fileName));
                if (!File.Exists(candidateLogical))
                {
                    continue;
                }

                var candidateCanonical = CanonicalizeExistingPath(candidateLogical);
                EnsureContained(rootCanonical, candidateCanonical);

                var info = new FileInfo(candidateCanonical);
                if (info.Length == 0 || info.Length > MaxContextFileBytes)
                {
                    continue;
                }

                await using var stream = new FileStream(
                    candidateCanonical,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                content = (await reader.ReadToEndAsync(cancellationToken)).Trim();
            }
            catch (Exception exception) when (exception is IOException
                                                 or UnauthorizedAccessException
                                                 or ArgumentException
                                                 or NotSupportedException
                                                 or PathTraversalException)
            {
                continue;
            }

            if (content.Length == 0 || totalChars + content.Length > MaxTotalContextChars)
            {
                continue;
            }

            sections.Add($"### {fileName}\n\n{content}");
            totalChars += content.Length;
        }

        if (sections.Count == 0)
        {
            return null;
        }

        return "## Contexto do workspace\n\n"
             + "Os arquivos abaixo descrevem o projeto e suas convenções; use-os como contexto.\n\n"
             + string.Join("\n\n", sections);
    }

    public async Task<IReadOnlyList<FileSearchResultDto>> SearchAsync(
        Guid workingDirectoryId,
        string rootAbsolutePath,
        string query,
        int limit,
        bool respectGitignore,
        CancellationToken cancellationToken)
    {
        RejectSuspiciousClientQuery(query);

        var rootCanonical = CanonicalizeExistingPath(rootAbsolutePath);
        var boundedLimit = Math.Clamp(limit, 1, 200);
        var normalizedQuery = NormalizeSearchQuery(query);
        var cacheKey = $"file-index:{workingDirectoryId}:{rootCanonical}:{respectGitignore}";

        var index = await cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(2);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return Task.FromResult(BuildIndex(rootCanonical, respectGitignore, cancellationToken));
        }) ?? Array.Empty<FileIndexEntry>();

        return SearchIndex(index, normalizedQuery, boundedLimit);
    }

    public Task<IReadOnlyList<FileReferenceValidationDto>> ValidateRelativePathsAsync(
        string rootAbsolutePath,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rootCanonical = CanonicalizeExistingPath(rootAbsolutePath);
        var results = relativePaths
            .Select(path => ValidateRelativePath(rootCanonical, path, cancellationToken))
            .ToList();

        return Task.FromResult<IReadOnlyList<FileReferenceValidationDto>>(results);
    }

    public Task<FileReferenceResolution> ResolveRelativePathAsync(
        string rootAbsolutePath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || HasParentTraversal(relativePath))
        {
            throw new PathTraversalException("Only relative paths inside the working directory are allowed.");
        }

        var rootCanonical = CanonicalizeExistingPath(rootAbsolutePath);
        var candidateLogical = Path.GetFullPath(Path.Combine(rootCanonical, NormalizeInputRelativePath(relativePath)));
        var exists = File.Exists(candidateLogical) || Directory.Exists(candidateLogical);
        var candidateCanonical = exists ? CanonicalizeExistingPath(candidateLogical) : TrimEndingDirectorySeparator(candidateLogical);
        EnsureContained(rootCanonical, candidateCanonical);

        var normalized = NormalizeRelativePath(Path.GetRelativePath(rootCanonical, candidateLogical));
        return Task.FromResult(new FileReferenceResolution(normalized, exists, dateTimeProvider.UtcNow));
    }

    private static FileReferenceValidationDto ValidateRelativePath(
        string rootCanonical,
        string rawPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var relativePath = rawPath.Trim().TrimStart('@');
        if (relativePath.Length == 0)
        {
            return new FileReferenceValidationDto(rawPath, string.Empty, false, false, "Path is required.");
        }

        try
        {
            if (Path.IsPathRooted(relativePath) || HasParentTraversal(relativePath))
            {
                return new FileReferenceValidationDto(
                    rawPath,
                    relativePath,
                    false,
                    false,
                    "Only relative paths inside the working directory are allowed.");
            }

            var candidateLogical = Path.GetFullPath(Path.Combine(rootCanonical, NormalizeInputRelativePath(relativePath)));
            var exists = File.Exists(candidateLogical) || Directory.Exists(candidateLogical);
            var candidateCanonical = exists
                ? CanonicalizeExistingPath(candidateLogical)
                : TrimEndingDirectorySeparator(candidateLogical);

            EnsureContained(rootCanonical, candidateCanonical);

            if (!exists)
            {
                return new FileReferenceValidationDto(
                    rawPath,
                    NormalizeRelativePath(Path.GetRelativePath(rootCanonical, candidateLogical)),
                    false,
                    false,
                    "File or directory was not found.");
            }

            var isDirectory = Directory.Exists(candidateCanonical);
            return new FileReferenceValidationDto(
                rawPath,
                NormalizeRelativePath(Path.GetRelativePath(rootCanonical, candidateCanonical)),
                true,
                isDirectory,
                null);
        }
        catch (Exception exception) when (exception is IOException
                                            or UnauthorizedAccessException
                                            or ArgumentException
                                            or NotSupportedException
                                            or PathTraversalException)
        {
            return new FileReferenceValidationDto(rawPath, relativePath, false, false, exception.Message);
        }
    }

    private static IReadOnlyList<FileIndexEntry> BuildIndex(
        string rootCanonical,
        bool respectGitignore,
        CancellationToken cancellationToken)
    {
        var matcher = respectGitignore ? BuildGitignoreMatcher(rootCanonical) : null;
        var indexEntries = new List<FileIndexEntry>();
        var directories = new Queue<string>();
        directories.Enqueue(rootCanonical);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDirectory = directories.Dequeue();

            IEnumerable<string> fileSystemEntries;
            try
            {
                fileSystemEntries = Directory.EnumerateFileSystemEntries(currentDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in fileSystemEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.System) != 0 || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var name = Path.GetFileName(entry);
                if (isDirectory && IgnoredDirectoryNames.Contains(name))
                {
                    continue;
                }

                var candidateCanonical = TrimEndingDirectorySeparator(Path.GetFullPath(entry));
                EnsureContained(rootCanonical, candidateCanonical);
                var relativePath = NormalizeRelativePath(Path.GetRelativePath(rootCanonical, entry));

                if (matcher is not null && !matcher.Match(relativePath).HasMatches)
                {
                    continue;
                }

                indexEntries.Add(new FileIndexEntry(relativePath, name, isDirectory));

                if (isDirectory)
                {
                    directories.Enqueue(entry);
                }
            }
        }

        return indexEntries;
    }

    private static IReadOnlyList<FileSearchResultDto> SearchIndex(
        IReadOnlyList<FileIndexEntry> index,
        string query,
        int limit)
    {
        return index
            .Select(entry => new
            {
                Entry = entry,
                Score = CalculateScore(entry.RelativePath, entry.FileName, query)
            })
            .Where(result => result.Score >= 0)
            .Select(result => new FileSearchResultDto(
                result.Entry.RelativePath,
                result.Entry.FileName,
                result.Entry.IsDirectory,
                result.Score))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativePath.Count(character => character == '/'))
            .ThenBy(result => result.RelativePath.Length)
            .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static int CalculateScore(string relativePath, string fileName, string query)
    {
        if (query.Length == 0)
        {
            return 1_000 - relativePath.Count(character => character == '/');
        }

        var normalizedFileName = fileName.ToUpperInvariant();
        var normalizedRelativePath = relativePath.ToUpperInvariant();
        var normalizedQuery = query.ToUpperInvariant();
        if (normalizedFileName == normalizedQuery)
        {
            return 10_000;
        }

        if (normalizedRelativePath == normalizedQuery)
        {
            return 9_500;
        }

        var exactFileNameIndex = normalizedFileName.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (exactFileNameIndex >= 0)
        {
            return 8_000 - exactFileNameIndex;
        }

        var exactPathIndex = normalizedRelativePath.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (exactPathIndex >= 0)
        {
            return 7_000 - exactPathIndex;
        }

        var pathScore = FuzzyScore(relativePath, query);
        var fileScore = FuzzyScore(fileName, query);
        if (pathScore < 0 && fileScore < 0)
        {
            return -1;
        }

        return Math.Max(pathScore, 0) + Math.Max(fileScore, 0) + (fileScore > 0 ? 100 : 0);
    }

    private static int FuzzyScore(string value, string query)
    {
        var valueIndex = 0;
        var score = 0;
        var previousMatchIndex = -2;

        foreach (var queryCharacter in query)
        {
            var found = false;
            while (valueIndex < value.Length)
            {
                if (char.ToUpperInvariant(value[valueIndex]) == char.ToUpperInvariant(queryCharacter))
                {
                    score += 10;
                    if (valueIndex == 0 || value[valueIndex - 1] is '/' or '\\' or '-' or '_' or '.')
                    {
                        score += 8;
                    }

                    if (valueIndex == previousMatchIndex + 1)
                    {
                        score += 5;
                    }

                    previousMatchIndex = valueIndex;
                    valueIndex++;
                    found = true;
                    break;
                }

                valueIndex++;
            }

            if (!found)
            {
                return -1;
            }
        }

        return score;
    }

    private static Matcher? BuildGitignoreMatcher(string rootCanonical)
    {
        var gitignorePath = Path.Combine(rootCanonical, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            return null;
        }

        var patterns = File.ReadAllLines(gitignorePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        if (patterns.Count == 0)
        {
            return null;
        }

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude("**");
        matcher.AddExcludePatterns(patterns);
        return matcher;
    }

    private static string CanonicalizeExistingPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new PathTraversalException("Path root could not be resolved.");
        var current = root;
        var remaining = fullPath[root.Length..]
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in remaining)
        {
            current = Path.Combine(current, segment);

            if (!File.Exists(current) && !Directory.Exists(current))
            {
                continue;
            }

            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);

            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                current = Path.GetFullPath(target.FullName);
            }
        }

        return TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    private static void EnsureContained(string rootCanonical, string candidateCanonical)
    {
        var relative = Path.GetRelativePath(rootCanonical, candidateCanonical);
        if (relative == ".")
        {
            return;
        }

        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new PathTraversalException("Path escapes the working directory.");
        }
    }

    private static void RejectSuspiciousClientQuery(string query)
    {
        var normalizedQuery = NormalizeSearchQuery(query);
        if (Path.IsPathRooted(normalizedQuery) || HasParentTraversal(normalizedQuery))
        {
            throw new PathTraversalException("Search query cannot be an absolute or parent-relative path.");
        }
    }

    private static string NormalizeSearchQuery(string query) =>
        query.Trim().TrimStart('@');

    private static bool HasParentTraversal(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment == "..");

    private static string NormalizeInputRelativePath(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/');

    private static string TrimEndingDirectorySeparator(string path) =>
        Path.TrimEndingDirectorySeparator(path);
}

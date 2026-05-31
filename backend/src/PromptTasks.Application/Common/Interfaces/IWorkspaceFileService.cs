using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Common.Interfaces;

public interface IWorkspaceFileService
{
    Task<ValidatedPathResult> ValidatePathAsync(string absolutePath, CancellationToken cancellationToken);

    Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken);

    Task<IReadOnlyList<FileSearchResultDto>> SearchAsync(
        Guid workingDirectoryId,
        string rootAbsolutePath,
        string query,
        int limit,
        bool respectGitignore,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FileReferenceValidationDto>> ValidateRelativePathsAsync(
        string rootAbsolutePath,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken);

    Task<FileReferenceResolution> ResolveRelativePathAsync(
        string rootAbsolutePath,
        string relativePath,
        CancellationToken cancellationToken);
}

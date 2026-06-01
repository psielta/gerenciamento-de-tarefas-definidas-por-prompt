using System.Globalization;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.WorkingDirectories;

namespace PromptTasks.Application.Common.Mappings;

public static class DtoMapper
{
    public static WorkingDirectoryDto ToDto(this WorkingDirectory workingDirectory) =>
        new(
            workingDirectory.Id,
            workingDirectory.Name,
            workingDirectory.AbsolutePath,
            workingDirectory.RespectGitignore,
            workingDirectory.EnableAiContext,
            workingDirectory.TaskNumberPattern,
            workingDirectory.CreatedAtUtc,
            workingDirectory.UpdatedAtUtc);

    public static PromptDto ToDto(this Prompt prompt, IEnumerable<PromptFileReference> references) =>
        new(
            prompt.Id,
            prompt.WorkingDirectoryId,
            prompt.ParentPromptId,
            prompt.TaskNumber,
            prompt.Title,
            prompt.Content,
            prompt.TargetAgent,
            prompt.Kind,
            prompt.Status,
            prompt.CurrentVersion,
            prompt.RowVersion.ToString(CultureInfo.InvariantCulture),
            prompt.CreatedAtUtc,
            prompt.UpdatedAtUtc,
            references
                .OrderBy(reference => reference.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Select(reference => new FileMentionDto(reference.RelativePath, reference.RawMention))
                .ToList());

    public static PromptVersionDto ToDto(this PromptVersion version) =>
        new(
            version.Id,
            version.PromptId,
            version.VersionNumber,
            version.Title,
            version.Content,
            version.TargetAgent,
            version.Kind,
            version.Status,
            version.ChangeNote,
            version.CreatedAtUtc);

    public static LinkedDocumentDto ToDto(this LinkedDocument document) =>
        new(
            document.Id,
            document.PromptId,
            document.WorkingDirectoryId,
            document.AbsolutePath,
            document.DisplayName ?? Path.GetFileName(document.AbsolutePath),
            document.DocumentType,
            document.Status,
            document.CurrentVersion,
            document.LastContentHash,
            document.SizeBytes,
            document.LastError,
            document.LastSyncedAtUtc,
            document.CreatedAtUtc,
            document.UpdatedAtUtc);

    public static LinkedDocumentVersionDto ToDto(this LinkedDocumentVersion version) =>
        new(
            version.Id,
            version.LinkedDocumentId,
            version.VersionNumber,
            version.ContentHash,
            version.SizeBytes,
            version.Source,
            version.CreatedAtUtc);

    public static LinkedDocumentContentDto ToContentDto(this LinkedDocumentVersion version) =>
        new(
            version.LinkedDocumentId,
            version.VersionNumber,
            version.Content,
            version.ContentHash,
            version.SizeBytes,
            version.CreatedAtUtc);
}

using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Models;
using PromptTasks.Domain.Ai;
using PromptTasks.Domain.Diagrams;
using PromptTasks.Domain.FutureTasks;
using PromptTasks.Domain.Notebooks;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.UnitTests;

/// <summary>
/// Shared in-memory fakes for the note/diagram AI generation handler tests.
/// Only the entity sets these handlers touch are backed by lists; everything
/// else returns empty.
/// </summary>
internal sealed class RecordingGeminiClient : IGeminiClient
{
    public GeminiGenerationRequest? LastRefineRequest { get; private set; }
    public string ResponseText { get; set; } = "generated";

    public Task<GeminiResult> RefineAsync(GeminiGenerationRequest request, CancellationToken ct)
    {
        LastRefineRequest = request;
        return Task.FromResult(new GeminiResult(ResponseText, 11, 7, 0));
    }

    public async IAsyncEnumerable<GeminiStreamChunk> StreamAsync(
        GeminiGenerationRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<GeminiCacheHandle?> EnsureSessionCacheAsync(
        string model,
        string systemInstruction,
        IReadOnlyList<GeminiTurn> history,
        CancellationToken ct) =>
        Task.FromResult<GeminiCacheHandle?>(null);

    public Task DeleteCacheAsync(string name, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class StubModelCatalog(bool includeModel = true) : IGeminiModelCatalog
{
    public const string ModelId = "gemini-test";

    private static readonly GeminiModelDto Model = new(ModelId, "Gemini Test", "none", true, 0, 0, 1024);

    public IReadOnlyList<GeminiModelDto> GetModels() =>
        includeModel ? new[] { Model } : Array.Empty<GeminiModelDto>();

    public GeminiModelDto? GetModel(string id) => includeModel && id == ModelId ? Model : null;
}

internal sealed class StubWorkspaceFileService : IWorkspaceFileService
{
    public string? Context { get; init; }
    public int ReadCount { get; private set; }

    public Task<ValidatedPathResult> ValidatePathAsync(string absolutePath, CancellationToken cancellationToken) =>
        Task.FromResult(ValidatedPathResult.Valid(absolutePath));

    public Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken)
    {
        ReadCount++;
        return Task.FromResult(Context);
    }

    public Task<string?> ReadSelectedFilesAsync(
        string rootAbsolutePath,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<IReadOnlyList<FileSearchResultDto>> SearchAsync(
        Guid workingDirectoryId,
        string rootAbsolutePath,
        string query,
        int limit,
        bool respectGitignore,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FileSearchResultDto>>(Array.Empty<FileSearchResultDto>());

    public Task<IReadOnlyList<FileReferenceValidationDto>> ValidateRelativePathsAsync(
        string rootAbsolutePath,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FileReferenceValidationDto>>(Array.Empty<FileReferenceValidationDto>());

    public Task<FileReferenceResolution> ResolveRelativePathAsync(
        string rootAbsolutePath,
        string relativePath,
        CancellationToken cancellationToken) =>
        Task.FromResult(new FileReferenceResolution(relativePath, true, DateTimeOffset.UtcNow));

    public Task<IReadOnlyList<DirectoryEntryDto>> BrowseDirectoryAsync(
        string rootAbsolutePath,
        string relativeDirectoryPath,
        bool respectGitignore,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DirectoryEntryDto>>(Array.Empty<DirectoryEntryDto>());

    public Task<FileContentDto> ReadFileAsync(
        string rootAbsolutePath,
        string relativePath,
        CancellationToken cancellationToken) =>
        Task.FromResult(new FileContentDto(relativePath, string.Empty, 0, false, false));
}

internal sealed class StubCurrentUser : ICurrentUser
{
    public Guid UserId { get; init; } = User.SystemUserId;
}

internal sealed class InMemoryAiDbContext : IApplicationDbContext
{
    public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
    public List<Notebook> NotebookItems { get; } = new();
    public List<Diagram> DiagramItems { get; } = new();

    public IQueryable<User> Users => Enumerable.Empty<User>().AsQueryable();
    public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
    public IQueryable<FutureTask> FutureTasks => Enumerable.Empty<FutureTask>().AsQueryable();
    public IQueryable<FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<FutureTaskLabel>().AsQueryable();
    public IQueryable<Prompt> Prompts => Enumerable.Empty<Prompt>().AsQueryable();
    public IQueryable<PromptVersion> PromptVersions => Enumerable.Empty<PromptVersion>().AsQueryable();
    public IQueryable<PromptFileReference> PromptFileReferences => Enumerable.Empty<PromptFileReference>().AsQueryable();
    public IQueryable<LinkedDocument> LinkedDocuments => Enumerable.Empty<LinkedDocument>().AsQueryable();
    public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => Enumerable.Empty<LinkedDocumentVersion>().AsQueryable();
    public IQueryable<WorkflowTemplate> WorkflowTemplates => Enumerable.Empty<WorkflowTemplate>().AsQueryable();
    public IQueryable<WorkflowTemplatePhase> WorkflowTemplatePhases => Enumerable.Empty<WorkflowTemplatePhase>().AsQueryable();
    public IQueryable<PromptWorkflow> PromptWorkflows => Enumerable.Empty<PromptWorkflow>().AsQueryable();
    public IQueryable<PromptWorkflowPhase> PromptWorkflowPhases => Enumerable.Empty<PromptWorkflowPhase>().AsQueryable();
    public IQueryable<PromptWorkflowEvent> PromptWorkflowEvents => Enumerable.Empty<PromptWorkflowEvent>().AsQueryable();
    public IQueryable<AiChatSession> AiChatSessions => Enumerable.Empty<AiChatSession>().AsQueryable();
    public IQueryable<AiChatMessage> AiChatMessages => Enumerable.Empty<AiChatMessage>().AsQueryable();
    public IQueryable<AiUserSettings> AiUserSettings => Enumerable.Empty<AiUserSettings>().AsQueryable();
    public IQueryable<Notebook> Notebooks => NotebookItems.AsQueryable();
    public IQueryable<Note> Notes => Enumerable.Empty<Note>().AsQueryable();
    public IQueryable<Diagram> Diagrams => DiagramItems.AsQueryable();

    public void Add<TEntity>(TEntity entity) where TEntity : class { }
    public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class { }
    public void Remove<TEntity>(TEntity entity) where TEntity : class { }
    public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class { }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public WorkingDirectory SeedWorkspace(bool enableAiContext, Guid? ownerId = null)
    {
        var workspace = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            RespectGitignore = true,
            EnableAiContext = enableAiContext,
            OwnerId = ownerId ?? User.SystemUserId,
        };
        WorkingDirectoryItems.Add(workspace);
        return workspace;
    }

    public Notebook SeedNotebook(Guid? workingDirectoryId = null, Guid? ownerId = null)
    {
        var notebook = new Notebook
        {
            Id = Guid.CreateVersion7(),
            Title = "Notebook",
            WorkingDirectoryId = workingDirectoryId,
            OwnerId = ownerId ?? User.SystemUserId,
        };
        NotebookItems.Add(notebook);
        return notebook;
    }

    public Diagram SeedDiagram(Guid workingDirectoryId, Guid? ownerId = null)
    {
        var diagram = new Diagram
        {
            Id = Guid.CreateVersion7(),
            WorkingDirectoryId = workingDirectoryId,
            Title = "Diagram",
            Type = DiagramType.Mermaid,
            Content = "flowchart TD\n  A --> B",
            OwnerId = ownerId ?? User.SystemUserId,
        };
        DiagramItems.Add(diagram);
        return diagram;
    }
}

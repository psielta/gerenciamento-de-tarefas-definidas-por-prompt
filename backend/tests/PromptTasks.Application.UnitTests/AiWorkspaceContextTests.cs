using FluentAssertions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.RefinePrompt;
using PromptTasks.Application.Features.Ai.Commands.SendChatMessage;
using PromptTasks.Application.Features.Ai.Models;
using PromptTasks.Application.Features.WorkingDirectories.Commands.UpdateWorkingDirectory;
using PromptTasks.Domain.Ai;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.UnitTests;

public sealed class AiWorkspaceContextTests
{
    [Fact]
    public async Task RefinePrompt_injects_workspace_context_when_enabled_and_owned()
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: true);
        var gemini = new FakeGeminiClient();
        var workspaceFiles = new FakeWorkspaceFileService { Context = "workspace context" };
        var handler = new RefinePromptHandler(
            gemini,
            new FakeModelCatalog(),
            context,
            workspaceFiles,
            new FakeCurrentUser());

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                workspace.Id,
                Array.Empty<string>(),
                null),
            CancellationToken.None);

        gemini.LastRefineRequest.Should().NotBeNull();
        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("workspace context");
        workspaceFiles.ReadCount.Should().Be(1);
    }

    [Fact]
    public async Task RefinePrompt_does_not_inject_workspace_context_when_disabled_or_not_owned()
    {
        var context = new FakeApplicationDbContext();
        var disabled = SeedWorkspace(context, enableAiContext: false);
        var otherOwner = SeedWorkspace(context, enableAiContext: true, ownerId: Guid.CreateVersion7());
        var gemini = new FakeGeminiClient();
        var workspaceFiles = new FakeWorkspaceFileService { Context = "workspace context" };
        var handler = new RefinePromptHandler(
            gemini,
            new FakeModelCatalog(),
            context,
            workspaceFiles,
            new FakeCurrentUser());

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                disabled.Id,
                Array.Empty<string>(),
                null),
            CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().NotContain("workspace context");

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                otherOwner.Id,
                Array.Empty<string>(),
                null),
            CancellationToken.None);

        gemini.LastRefineRequest!.SystemInstruction.Should().NotContain("workspace context");
        workspaceFiles.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task RefinePrompt_injects_selected_files_when_workspace_context_is_disabled()
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: false);
        var gemini = new FakeGeminiClient();
        var workspaceFiles = new FakeWorkspaceFileService { SelectedFilesContext = "selected files context" };
        var handler = new RefinePromptHandler(
            gemini,
            new FakeModelCatalog(),
            context,
            workspaceFiles,
            new FakeCurrentUser());

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                workspace.Id,
                new[] { "src/main.cs" },
                null),
            CancellationToken.None);

        gemini.LastRefineRequest.Should().NotBeNull();
        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("selected files context");
        workspaceFiles.ReadCount.Should().Be(0);
        workspaceFiles.ReadSelectedFilesCount.Should().Be(1);
        workspaceFiles.LastSelectedPaths.Should().Equal("src/main.cs");
    }

    [Fact]
    public async Task RefinePrompt_injects_custom_instructions()
    {
        var context = new FakeApplicationDbContext();
        var gemini = new FakeGeminiClient();
        var handler = new RefinePromptHandler(
            gemini,
            new FakeModelCatalog(),
            context,
            new FakeWorkspaceFileService(),
            new FakeCurrentUser());

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                null,
                Array.Empty<string>(),
                "Focus on data validation"),
            CancellationToken.None);

        gemini.LastRefineRequest.Should().NotBeNull();
        gemini.LastRefineRequest!.SystemInstruction.Should().Contain("Instruções adicionais do usuário");
        gemini.LastRefineRequest.SystemInstruction.Should().Contain("Focus on data validation");
    }

    [Fact]
    public async Task RefinePrompt_combines_workspace_context_selected_files_and_custom_instructions()
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: true);
        var gemini = new FakeGeminiClient();
        var workspaceFiles = new FakeWorkspaceFileService
        {
            Context = "workspace context",
            SelectedFilesContext = "selected files context"
        };
        var handler = new RefinePromptHandler(
            gemini,
            new FakeModelCatalog(),
            context,
            workspaceFiles,
            new FakeCurrentUser());

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                workspace.Id,
                new[] { "src/main.cs" },
                "Keep it concise"),
            CancellationToken.None);

        var instruction = gemini.LastRefineRequest!.SystemInstruction;
        instruction.Should().Contain("workspace context");
        instruction.Should().Contain("selected files context");
        instruction.Should().Contain("Keep it concise");
        instruction.IndexOf("workspace context", StringComparison.Ordinal)
            .Should().BeLessThan(instruction.IndexOf("selected files context", StringComparison.Ordinal));
        instruction.IndexOf("selected files context", StringComparison.Ordinal)
            .Should().BeLessThan(instruction.IndexOf("Keep it concise", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefinePrompt_does_not_read_selected_files_from_workspace_owned_by_other_user()
    {
        var context = new FakeApplicationDbContext();
        var otherOwner = SeedWorkspace(context, enableAiContext: false, ownerId: Guid.CreateVersion7());
        var gemini = new FakeGeminiClient();
        var workspaceFiles = new FakeWorkspaceFileService { SelectedFilesContext = "selected files context" };
        var handler = new RefinePromptHandler(
            gemini,
            new FakeModelCatalog(),
            context,
            workspaceFiles,
            new FakeCurrentUser());

        await handler.Handle(
            new RefinePromptCommand(
                "Improve this",
                FakeModelCatalog.ModelId,
                0.4,
                new GeminiThinking("none", 0, null),
                otherOwner.Id,
                new[] { "src/main.cs" },
                null),
            CancellationToken.None);

        workspaceFiles.ReadSelectedFilesCount.Should().Be(0);
        gemini.LastRefineRequest!.SystemInstruction.Should().NotContain("selected files context");
    }

    [Fact]
    public async Task SendChatMessage_seeds_session_cache_with_workspace_context()
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: true);
        var session = new AiChatSession
        {
            Id = Guid.CreateVersion7(),
            OwnerId = User.SystemUserId,
            WorkingDirectoryId = workspace.Id,
            Model = FakeModelCatalog.ModelId,
            Temperature = 0.7,
            ThinkingEnabled = false,
            Title = "Chat"
        };
        context.AiChatSessionItems.Add(session);
        var gemini = new FakeGeminiClient();
        var handler = new SendChatMessageHandler(
            context,
            gemini,
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            new FakeWorkspaceFileService { Context = "workspace context" });

        var chunks = new List<ChatChunkDto>();
        await foreach (var chunk in handler.Handle(
                           new SendChatMessageCommand(session.Id, "hello", false, null),
                           CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        chunks.Should().Contain(chunk => chunk.Text == "model response");
        gemini.LastStreamRequest.Should().NotBeNull();
        gemini.LastStreamRequest!.UseSystemCache.Should().BeFalse();
        gemini.LastStreamRequest.SystemInstruction.Should().Contain("workspace context");
        gemini.LastCacheSystemInstruction.Should().Contain("workspace context");
        session.GeminiCacheName.Should().Be("cached/session");
        session.CacheSystemInstructionHash.Should().NotBeNullOrWhiteSpace();
        session.CacheSystemInstructionHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task SendChatMessage_reuses_session_cache_when_workspace_context_hash_matches()
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: true);
        var session = new AiChatSession
        {
            Id = Guid.CreateVersion7(),
            OwnerId = User.SystemUserId,
            WorkingDirectoryId = workspace.Id,
            Model = FakeModelCatalog.ModelId,
            Temperature = 0.7,
            ThinkingEnabled = false,
            Title = "Chat"
        };
        context.AiChatSessionItems.Add(session);
        var gemini = new FakeGeminiClient();
        var handler = new SendChatMessageHandler(
            context,
            gemini,
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            new FakeWorkspaceFileService { Context = "workspace context" });

        await DrainAsync(handler.Handle(
            new SendChatMessageCommand(session.Id, "first", false, null),
            CancellationToken.None));

        session.GeminiCacheName.Should().Be("cached/session");
        session.CacheSystemInstructionHash.Should().NotBeNullOrWhiteSpace();
        session.CachedThroughSequence.Should().Be(2);
        gemini.EnsureSessionCacheCallCount.Should().Be(1);

        await DrainAsync(handler.Handle(
            new SendChatMessageCommand(session.Id, "second", false, null),
            CancellationToken.None));

        gemini.LastStreamRequest.Should().NotBeNull();
        gemini.LastStreamRequest!.CachedContentName.Should().Be("cached/session");
        gemini.LastStreamRequest.UseSystemCache.Should().BeFalse();
        gemini.LastStreamRequest.Contents.Should().ContainSingle();
        gemini.LastStreamRequest.Contents.Should().ContainSingle(turn => turn.Role == "user" && turn.Text == "second");
        gemini.EnsureSessionCacheCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendChatMessage_does_not_reuse_session_cache_when_workspace_context_hash_changed()
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: true);
        var session = new AiChatSession
        {
            Id = Guid.CreateVersion7(),
            OwnerId = User.SystemUserId,
            WorkingDirectoryId = workspace.Id,
            Model = FakeModelCatalog.ModelId,
            Temperature = 0.7,
            ThinkingEnabled = false,
            Title = "Chat",
            GeminiCacheName = "cached/old-session",
            CacheExpiresAt = new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero),
            CacheSystemInstructionHash = "old-hash",
            CachedThroughSequence = 1
        };
        context.AiChatSessionItems.Add(session);
        context.AiChatMessageItems.Add(new AiChatMessage
        {
            Id = Guid.CreateVersion7(),
            SessionId = session.Id,
            Role = "user",
            Content = "previous",
            Sequence = 1,
            CreatedAtUtc = new DateTimeOffset(2026, 6, 1, 11, 59, 0, TimeSpan.Zero)
        });
        var gemini = new FakeGeminiClient();
        var handler = new SendChatMessageHandler(
            context,
            gemini,
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            new FakeWorkspaceFileService { Context = "new workspace context" });

        await foreach (var _ in handler.Handle(
                           new SendChatMessageCommand(session.Id, "hello", false, null),
                           CancellationToken.None))
        {
        }

        gemini.LastStreamRequest.Should().NotBeNull();
        gemini.LastStreamRequest!.CachedContentName.Should().BeNull();
        gemini.LastStreamRequest.UseSystemCache.Should().BeFalse();
        gemini.LastStreamRequest.Contents.Should().Contain(turn => turn.Text == "previous");
        gemini.EnsureSessionCacheCallCount.Should().Be(1);
        gemini.LastCacheSystemInstruction.Should().Contain("new workspace context");
        session.GeminiCacheName.Should().Be("cached/session");
        session.CacheSystemInstructionHash.Should().NotBe("old-hash");
    }

    [Fact]
    public async Task SendChatMessage_seeds_session_cache_with_fallback_instruction_without_workspace_context()
    {
        var context = new FakeApplicationDbContext();
        var session = new AiChatSession
        {
            Id = Guid.CreateVersion7(),
            OwnerId = User.SystemUserId,
            Model = FakeModelCatalog.ModelId,
            Temperature = 0.7,
            ThinkingEnabled = false,
            Title = "Chat"
        };
        context.AiChatSessionItems.Add(session);
        var workspaceFiles = new FakeWorkspaceFileService { Context = "workspace context" };
        var gemini = new FakeGeminiClient();
        var handler = new SendChatMessageHandler(
            context,
            gemini,
            new FakeCurrentUser(),
            new FakeDateTimeProvider(),
            workspaceFiles);

        await DrainAsync(handler.Handle(
            new SendChatMessageCommand(session.Id, "hello", false, null),
            CancellationToken.None));

        workspaceFiles.ReadCount.Should().Be(0);
        gemini.LastStreamRequest.Should().NotBeNull();
        gemini.LastStreamRequest!.UseSystemCache.Should().BeTrue();
        gemini.LastStreamRequest.SystemInstruction.Should().Contain("assistente especializado");
        gemini.LastCacheSystemInstruction.Should().Contain("assistente especializado");
        session.CacheSystemInstructionHash.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(false, true, "C:/repo", true)]
    [InlineData(true, false, "C:/repo", true)]
    [InlineData(true, true, "C:/repo-new", true)]
    [InlineData(false, false, "C:/repo-new", false)]
    public async Task UpdateWorkingDirectory_resets_session_cache_only_when_ai_context_cache_can_be_stale(
        bool currentEnabled,
        bool nextEnabled,
        string nextPath,
        bool shouldReset)
    {
        var context = new FakeApplicationDbContext();
        var workspace = SeedWorkspace(context, enableAiContext: currentEnabled);
        workspace.AbsolutePath = "C:/repo";
        var session = new AiChatSession
        {
            Id = Guid.CreateVersion7(),
            OwnerId = User.SystemUserId,
            WorkingDirectoryId = workspace.Id,
            GeminiCacheName = "cached/session",
            CacheExpiresAt = new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero),
            CacheSystemInstructionHash = "existing-hash",
            CachedThroughSequence = 10
        };
        context.AiChatSessionItems.Add(session);
        var gemini = new FakeGeminiClient();
        var handler = new UpdateWorkingDirectoryHandler(
            context,
            new FakeWorkspaceFileService { CanonicalPath = nextPath },
            new FakeCurrentUser(),
            gemini);

        await handler.Handle(
            new UpdateWorkingDirectoryCommand(workspace.Id, "repo", nextPath, true, nextEnabled),
            CancellationToken.None);

        if (shouldReset)
        {
            session.GeminiCacheName.Should().BeNull();
            session.CacheExpiresAt.Should().BeNull();
            session.CacheSystemInstructionHash.Should().BeNull();
            session.CachedThroughSequence.Should().Be(0);
            gemini.DeletedCacheNames.Should().ContainSingle("cached/session");
        }
        else
        {
            session.GeminiCacheName.Should().Be("cached/session");
            session.CacheSystemInstructionHash.Should().Be("existing-hash");
            session.CachedThroughSequence.Should().Be(10);
            gemini.DeletedCacheNames.Should().BeEmpty();
        }
    }

    private static WorkingDirectory SeedWorkspace(
        FakeApplicationDbContext context,
        bool enableAiContext,
        Guid? ownerId = null)
    {
        var workspace = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            RespectGitignore = true,
            EnableAiContext = enableAiContext,
            OwnerId = ownerId ?? User.SystemUserId
        };
        context.WorkingDirectoryItems.Add(workspace);
        return workspace;
    }

    private static async Task DrainAsync(IAsyncEnumerable<ChatChunkDto> chunks)
    {
        await foreach (var _ in chunks)
        {
        }
    }

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public List<User> UserItems { get; } = new();
        public List<WorkingDirectory> WorkingDirectoryItems { get; } = new();
        public List<Prompt> PromptItems { get; } = new();
        public List<PromptVersion> PromptVersionItems { get; } = new();
        public List<PromptFileReference> PromptFileReferenceItems { get; } = new();
        public List<LinkedDocument> LinkedDocumentItems { get; } = new();
        public List<LinkedDocumentVersion> LinkedDocumentVersionItems { get; } = new();
        public List<WorkflowTemplate> WorkflowTemplateItems { get; } = new();
        public List<WorkflowTemplatePhase> WorkflowTemplatePhaseItems { get; } = new();
        public List<PromptWorkflow> PromptWorkflowItems { get; } = new();
        public List<PromptWorkflowPhase> PromptWorkflowPhaseItems { get; } = new();
        public List<PromptWorkflowEvent> PromptWorkflowEventItems { get; } = new();
        public List<AiChatSession> AiChatSessionItems { get; } = new();
        public List<AiChatMessage> AiChatMessageItems { get; } = new();
        public List<AiUserSettings> AiUserSettingsItems { get; } = new();

        public IQueryable<User> Users => UserItems.AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.FutureTasks.FutureTask> FutureTasks => Enumerable.Empty<PromptTasks.Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<PromptTasks.Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<PromptTasks.Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Prompt> Prompts => PromptItems.AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => PromptVersionItems.AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => PromptFileReferenceItems.AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => LinkedDocumentItems.AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => LinkedDocumentVersionItems.AsQueryable();
        public IQueryable<WorkflowTemplate> WorkflowTemplates => WorkflowTemplateItems.AsQueryable();
        public IQueryable<WorkflowTemplatePhase> WorkflowTemplatePhases => WorkflowTemplatePhaseItems.AsQueryable();
        public IQueryable<PromptWorkflow> PromptWorkflows => PromptWorkflowItems.AsQueryable();
        public IQueryable<PromptWorkflowPhase> PromptWorkflowPhases => PromptWorkflowPhaseItems.AsQueryable();
        public IQueryable<PromptWorkflowEvent> PromptWorkflowEvents => PromptWorkflowEventItems.AsQueryable();
        public IQueryable<AiChatSession> AiChatSessions => AiChatSessionItems.AsQueryable();
        public IQueryable<AiChatMessage> AiChatMessages => AiChatMessageItems.AsQueryable();
        public IQueryable<AiUserSettings> AiUserSettings => AiUserSettingsItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<PromptTasks.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Note> Notes => Enumerable.Empty<PromptTasks.Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Diagrams.Diagram> Diagrams => Enumerable.Empty<PromptTasks.Domain.Diagrams.Diagram>().AsQueryable();

        public int SaveChangesCount { get; private set; }

        public void Add<TEntity>(TEntity entity) where TEntity : class => Route(entity, add: true);

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        public void Remove<TEntity>(TEntity entity) where TEntity : class => Route(entity, add: false);

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities.ToList())
            {
                Remove(entity);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        private void Route<TEntity>(TEntity entity, bool add) where TEntity : class
        {
            switch (entity)
            {
                case WorkingDirectory item: Apply(WorkingDirectoryItems, item, add); break;
                case Prompt item: Apply(PromptItems, item, add); break;
                case PromptVersion item: Apply(PromptVersionItems, item, add); break;
                case PromptFileReference item: Apply(PromptFileReferenceItems, item, add); break;
                case LinkedDocument item: Apply(LinkedDocumentItems, item, add); break;
                case LinkedDocumentVersion item: Apply(LinkedDocumentVersionItems, item, add); break;
                case WorkflowTemplate item: Apply(WorkflowTemplateItems, item, add); break;
                case WorkflowTemplatePhase item: Apply(WorkflowTemplatePhaseItems, item, add); break;
                case PromptWorkflow item: Apply(PromptWorkflowItems, item, add); break;
                case PromptWorkflowPhase item: Apply(PromptWorkflowPhaseItems, item, add); break;
                case PromptWorkflowEvent item: Apply(PromptWorkflowEventItems, item, add); break;
                case AiChatSession item: Apply(AiChatSessionItems, item, add); break;
                case AiChatMessage item: Apply(AiChatMessageItems, item, add); break;
                case AiUserSettings item: Apply(AiUserSettingsItems, item, add); break;
            }
        }

        private static void Apply<T>(List<T> list, T entity, bool add)
        {
            if (add)
            {
                list.Add(entity);
            }
            else
            {
                list.Remove(entity);
            }
        }
    }

    private sealed class FakeWorkspaceFileService : IWorkspaceFileService
    {
        public string? Context { get; init; }
        public string? SelectedFilesContext { get; init; }
        public string? CanonicalPath { get; init; }
        public int ReadCount { get; private set; }
        public int ReadSelectedFilesCount { get; private set; }
        public IReadOnlyList<string> LastSelectedPaths { get; private set; } = Array.Empty<string>();

        public Task<ValidatedPathResult> ValidatePathAsync(string absolutePath, CancellationToken cancellationToken) =>
            Task.FromResult(ValidatedPathResult.Valid(CanonicalPath ?? absolutePath));

        public Task<string?> ReadWorkspaceContextAsync(string rootAbsolutePath, CancellationToken cancellationToken)
        {
            ReadCount++;
            return Task.FromResult(Context);
        }

        public Task<string?> ReadSelectedFilesAsync(
            string rootAbsolutePath,
            IReadOnlyList<string> relativePaths,
            CancellationToken cancellationToken)
        {
            ReadSelectedFilesCount++;
            LastSelectedPaths = relativePaths.ToList();
            return Task.FromResult(SelectedFilesContext);
        }

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

    private sealed class FakeGeminiClient : IGeminiClient
    {
        public GeminiGenerationRequest? LastRefineRequest { get; private set; }
        public GeminiGenerationRequest? LastStreamRequest { get; private set; }
        public string? LastCacheSystemInstruction { get; private set; }
        public int EnsureSessionCacheCallCount { get; private set; }
        public List<string> DeletedCacheNames { get; } = new();

        public Task<GeminiResult> RefineAsync(GeminiGenerationRequest request, CancellationToken ct)
        {
            LastRefineRequest = request;
            return Task.FromResult(new GeminiResult("refined", 10, 5, 0));
        }

        public async IAsyncEnumerable<GeminiStreamChunk> StreamAsync(
            GeminiGenerationRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            LastStreamRequest = request;
            yield return new GeminiStreamChunk("model response", false, false, null);
            yield return new GeminiStreamChunk(string.Empty, false, true, null);
            await Task.CompletedTask;
        }

        public Task<GeminiCacheHandle?> EnsureSessionCacheAsync(
            string model,
            string systemInstruction,
            IReadOnlyList<GeminiTurn> history,
            CancellationToken ct)
        {
            EnsureSessionCacheCallCount++;
            LastCacheSystemInstruction = systemInstruction;
            return Task.FromResult<GeminiCacheHandle?>(
                new GeminiCacheHandle("cached/session", new DateTimeOffset(2026, 6, 1, 12, 30, 0, TimeSpan.Zero)));
        }

        public Task DeleteCacheAsync(string name, CancellationToken ct)
        {
            DeletedCacheNames.Add(name);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeModelCatalog : IGeminiModelCatalog
    {
        public const string ModelId = "gemini-test";

        private static readonly GeminiModelDto Model = new(
            ModelId,
            "Gemini Test",
            "none",
            true,
            0,
            0,
            1024);

        public IReadOnlyList<GeminiModelDto> GetModels() => new[] { Model };

        public GeminiModelDto? GetModel(string id) => id == ModelId ? Model : null;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    }
}

using FluentAssertions;
using FluentValidation;
using PromptTasks.Application.Common.Behaviors;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.PromptTemplates;
using PromptTasks.Application.Features.PromptTemplates.Commands.GeneratePromptDraft;
using PromptTasks.Application.Features.PromptTemplates.Definitions;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.UnitTests;

public sealed class PromptTemplateHandlerTests
{
    [Fact]
    public void Catalog_returns_templates_ordered_by_key()
    {
        var catalog = CreateCatalog();

        var templates = catalog.GetAll();

        templates.Select(template => template.Key).Should()
            .Equal(
                PromptTemplateKey.ReviewPlan,
                PromptTemplateKey.ImplementPlan,
                PromptTemplateKey.ReviewPlanWithParentPrompt,
                PromptTemplateKey.ReReviewPlan,
                PromptTemplateKey.ImplementPlanInWorktree,
                PromptTemplateKey.ReviewPullRequest,
                PromptTemplateKey.ReReviewPullRequest,
                PromptTemplateKey.RebaseCurrentBranch,
                PromptTemplateKey.MergePullRequest);
        catalog.Get(PromptTemplateKey.ReviewPlan).Should().BeOfType<ReviewPlanTemplate>();
        catalog.Get(PromptTemplateKey.ImplementPlan).Should().BeOfType<ImplementPlanTemplate>();
        catalog.Get(PromptTemplateKey.ReviewPlanWithParentPrompt).Should().BeOfType<ReviewPlanWithParentPromptTemplate>();
        catalog.Get(PromptTemplateKey.ReReviewPlan).Should().BeOfType<ReReviewPlanTemplate>();
        catalog.Get(PromptTemplateKey.ImplementPlanInWorktree).Should().BeOfType<ImplementPlanInWorktreeTemplate>();
        catalog.Get(PromptTemplateKey.ReviewPullRequest).Should().BeOfType<ReviewPullRequestTemplate>();
        catalog.Get(PromptTemplateKey.ReReviewPullRequest).Should().BeOfType<ReReviewPullRequestTemplate>();
        catalog.Get(PromptTemplateKey.MergePullRequest).Should().BeOfType<MergePullRequestTemplate>();
        catalog.Get(PromptTemplateKey.RebaseCurrentBranch).Should().BeOfType<RebaseCurrentBranchTemplate>();
    }

    [Theory]
    [InlineData(PromptTemplateKey.ReviewPlan, WorkflowPhaseRole.PlanReview, false)]
    [InlineData(PromptTemplateKey.ReviewPlanWithParentPrompt, WorkflowPhaseRole.PlanReview, false)]
    [InlineData(PromptTemplateKey.ReReviewPlan, WorkflowPhaseRole.PlanReview, true)]
    [InlineData(PromptTemplateKey.ImplementPlan, WorkflowPhaseRole.Implementation, false)]
    [InlineData(PromptTemplateKey.ImplementPlanInWorktree, WorkflowPhaseRole.Implementation, false)]
    [InlineData(PromptTemplateKey.ReviewPullRequest, WorkflowPhaseRole.CodeReview, false)]
    [InlineData(PromptTemplateKey.ReReviewPullRequest, WorkflowPhaseRole.CodeReview, true)]
    [InlineData(PromptTemplateKey.RebaseCurrentBranch, WorkflowPhaseRole.Rebase, false)]
    [InlineData(PromptTemplateKey.MergePullRequest, WorkflowPhaseRole.Merge, false)]
    public void Templates_expose_workflow_phase_metadata(
        PromptTemplateKey key,
        WorkflowPhaseRole targetRole,
        bool isReReview)
    {
        var template = CreateCatalog().Get(key);

        template.TargetPhaseRole.Should().Be(targetRole);
        template.IsReReview.Should().Be(isReReview);
    }

    [Fact]
    public void Catalog_throws_for_unknown_template_key()
    {
        var catalog = CreateCatalog();

        var act = () => catalog.Get((PromptTemplateKey)999);

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public async Task GeneratePromptDraft_review_plan_uses_linked_document_path_and_parent_workspace()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/Users/psiel/.claude/plans/plan.md", "plan.md");
        context.LinkedDocumentVersionItems.Add(new LinkedDocumentVersion
        {
            LinkedDocumentId = document.Id,
            VersionNumber = 1,
            Content = "# Saved plan",
            ContentHash = "hash",
            SizeBytes = 12
        });
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ReviewPlan),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.ReviewPlan);
        result.LinkedDocumentId.Should().Be(document.Id);
        result.WorkingDirectoryId.Should().Be(prompt.WorkingDirectoryId);
        result.ParentPromptId.Should().Be(prompt.Id);
        result.Title.Should().Be("Review plan: plan.md");
        result.Content.Should().Be(
            "Given the plan \"C:/Users/psiel/.claude/plans/plan.md\", validate the plan, approve it, or point out improvements.");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.Planning);
        context.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task GeneratePromptDraft_implement_plan_uses_general_kind()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/implementation.md", "implementation.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ImplementPlan),
            CancellationToken.None);

        result.Title.Should().Be("Implement plan: implementation.md");
        result.Content.Should().Be("Implement the plan \"C:/plans/implementation.md\".");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.General);
    }

    [Fact]
    public async Task GeneratePromptDraft_review_plan_with_parent_prompt_includes_original_prompt()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId, "Faca um plano para @src/main.go");
        var document = SeedLinkedDocument(context, prompt, "C:/plans/plan.md", "plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ReviewPlanWithParentPrompt),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.ReviewPlanWithParentPrompt);
        result.Title.Should().Be("Review plan with parent prompt: plan.md");
        result.Content.Should().Be(
            """
            I asked Claude to run plan-mode using the prompt below:

            ```md
            Faca um plano para @src/main.go
            ```

            It generated the plan "C:/plans/plan.md".

            Given the plan "C:/plans/plan.md", validate the plan, approve it, or point out improvements.
            """);
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.Planning);
    }

    [Fact]
    public async Task GeneratePromptDraft_re_review_plan_explains_it_is_a_second_validation()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/reviewed-plan.md", "reviewed-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ReReviewPlan),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.ReReviewPlan);
        result.Title.Should().Be("Re-review plan: reviewed-plan.md");
        result.Content.Should().Be(
            "I passed the previous points to Claude to fix in the plan \"C:/plans/reviewed-plan.md\". Validate the updated plan again, approve it if correct, or point out the improvements that are still missing.");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.Planning);
    }

    [Fact]
    public async Task GeneratePromptDraft_implement_plan_in_worktree_requests_pr_creation()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/worktree-plan.md", "worktree-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ImplementPlanInWorktree),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.ImplementPlanInWorktree);
        result.Title.Should().Be("Implement in worktree: worktree-plan.md");
        result.Content.Should().Contain("Implement the plan `C:/plans/worktree-plan.md` completely in a separate worktree.");
        result.Content.Should().Contain("open a PR");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.General);
    }

    [Fact]
    public async Task GeneratePromptDraft_review_pull_request_uses_pr_reference()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/pr-plan.md", "pr-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ReviewPullRequest, "123"),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.ReviewPullRequest);
        result.Title.Should().Be("Review PR #123: pr-plan.md");
        result.Content.Should().StartWith("/review");
        result.Content.Should().Contain("Review the PR #123 that implements the plan `C:/plans/pr-plan.md`.");
        result.Content.Should().Contain("Prioritize bugs, behavioral risks, and missing tests.");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.General);
    }

    [Fact]
    public async Task GeneratePromptDraft_merge_pull_request_uses_pr_reference()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/merge-plan.md", "merge-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.MergePullRequest, "123"),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.MergePullRequest);
        result.Title.Should().Be("Merge PR #123: merge-plan.md");
        result.Content.Should().Contain("Merge the PR #123 that implements the plan `C:/plans/merge-plan.md`.");
        result.Content.Should().Contain("sync the local main branch with the remote");
        result.Content.Should().Contain("remove the worktree if it exists");
        result.Content.Should().Contain("delete the local/remote branch if they still exist and it is safe");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.General);
    }

    [Fact]
    public async Task GeneratePromptDraft_re_review_pull_request_uses_pr_reference_and_codex_response()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/pr-plan.md", "pr-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(
                document.Id,
                PromptTemplateKey.ReReviewPullRequest,
                Inputs: new Dictionary<string, string>
                {
                    ["pullRequest"] = "123",
                    ["codexResponse"] = "Codex fixed the missing regression test."
                }),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.ReReviewPullRequest);
        result.Title.Should().Be("Re-review PR #123: pr-plan.md");
        result.Content.Should().StartWith("/review");
        result.Content.Should().Contain("Re-review the PR #123 after Codex made fixes for the previous review findings.");
        result.Content.Should().Contain("The PR implements the plan `C:/plans/pr-plan.md`.");
        result.Content.Should().Contain("Codex response after applying fixes:");
        result.Content.Should().Contain("Codex fixed the missing regression test.");
        result.Content.Should().Contain("Treat the Codex response as a handoff, not proof.");
        result.Content.Should().Contain("If the PR is now acceptable, say that clearly.");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.General);
    }

    [Fact]
    public async Task GeneratePromptDraft_rebase_current_branch_requests_rebase_from_remote_main()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/rebase-plan.md", "rebase-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.RebaseCurrentBranch),
            CancellationToken.None);

        result.TemplateKey.Should().Be(PromptTemplateKey.RebaseCurrentBranch);
        result.Title.Should().Be("Update branch from main: rebase-plan.md");
        result.Content.Should().Contain("Update my current branch/worktree with the latest changes from the remote main branch using rebase.");
        result.Content.Should().Contain("Preserve unrelated local changes.");
        result.Content.Should().Contain("If there are conflicts, stop and tell me so we can resolve them together.");
        result.TargetAgent.Should().Be(TargetAgent.Codex);
        result.Kind.Should().Be(PromptKind.General);
    }

    [Fact]
    public async Task GeneratePromptDraft_rejects_document_from_another_owner()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, Guid.CreateVersion7());
        var document = SeedLinkedDocument(context, prompt, "C:/plans/other.md", "other.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var act = () => handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ReviewPlan),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GeneratePromptDraft_validation_rejects_invalid_template_key()
    {
        var behavior = new ValidationBehavior<GeneratePromptDraftCommand, GeneratedPromptDraftDto>(
            new[] { new GeneratePromptDraftValidator() });
        var invalid = new GeneratePromptDraftCommand(Guid.CreateVersion7(), (PromptTemplateKey)999);

        var act = () => behavior.Handle(
            invalid,
            _ => Task.FromResult(new GeneratedPromptDraftDto(
                invalid.TemplateKey,
                invalid.LinkedDocumentId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "",
                "",
                TargetAgent.Codex,
                PromptKind.General)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Theory]
    [InlineData(PromptTemplateKey.ReviewPullRequest)]
    [InlineData(PromptTemplateKey.ReReviewPullRequest)]
    [InlineData(PromptTemplateKey.MergePullRequest)]
    public async Task GeneratePromptDraft_requires_pull_request_when_plan_has_none(PromptTemplateKey templateKey)
    {
        // A obrigatoriedade da PR foi movida para o handler (apos o fallback do plano vinculado).
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/pr-plan.md", "pr-plan.md");
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        var act = () => handler.Handle(
            new GeneratePromptDraftCommand(document.Id, templateKey),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GeneratePromptDraft_pulls_pull_request_from_linked_document()
    {
        var context = new FakeApplicationDbContext();
        var prompt = SeedPrompt(context, User.SystemUserId);
        var document = SeedLinkedDocument(context, prompt, "C:/plans/pr-plan.md", "pr-plan.md");
        document.PullRequestReference = "123";
        var handler = new GeneratePromptDraftHandler(context, CreateCatalog(), new FakeCurrentUser());

        // Sem PR na request: deve puxar a PR salva no plano vinculado.
        var result = await handler.Handle(
            new GeneratePromptDraftCommand(document.Id, PromptTemplateKey.ReviewPullRequest),
            CancellationToken.None);

        result.Title.Should().Be("Review PR #123: pr-plan.md");
        result.Content.Should().Contain("Review the PR #123 that implements the plan `C:/plans/pr-plan.md`.");
    }

    [Fact]
    public async Task GeneratePromptDraft_validation_requires_codex_response_for_re_review_pull_request_template()
    {
        var behavior = new ValidationBehavior<GeneratePromptDraftCommand, GeneratedPromptDraftDto>(
            new[] { new GeneratePromptDraftValidator() });
        var invalid = new GeneratePromptDraftCommand(Guid.CreateVersion7(), PromptTemplateKey.ReReviewPullRequest, "123");

        var act = () => behavior.Handle(
            invalid,
            _ => Task.FromResult(new GeneratedPromptDraftDto(
                invalid.TemplateKey,
                invalid.LinkedDocumentId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "",
                "",
                TargetAgent.Codex,
                PromptKind.General)),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private static PromptTemplateCatalog CreateCatalog() =>
        new(new IPromptTemplateDefinition[]
        {
            new ImplementPlanTemplate(),
            new ReviewPlanTemplate(),
            new ReviewPlanWithParentPromptTemplate(),
            new ReReviewPlanTemplate(),
            new ImplementPlanInWorktreeTemplate(),
            new ReviewPullRequestTemplate(),
            new ReReviewPullRequestTemplate(),
            new MergePullRequestTemplate(),
            new RebaseCurrentBranchTemplate()
        });

    private static Prompt SeedPrompt(
        FakeApplicationDbContext context,
        Guid ownerId,
        string content = "Content")
    {
        var directory = new WorkingDirectory
        {
            Id = Guid.CreateVersion7(),
            Name = "repo",
            AbsolutePath = "C:/repo",
            OwnerId = ownerId
        };
        var prompt = new Prompt
        {
            Id = Guid.CreateVersion7(),
            WorkingDirectoryId = directory.Id,
            Title = "Prompt",
            Content = content,
            OwnerId = ownerId
        };

        context.WorkingDirectoryItems.Add(directory);
        context.PromptItems.Add(prompt);
        return prompt;
    }

    private static LinkedDocument SeedLinkedDocument(
        FakeApplicationDbContext context,
        Prompt prompt,
        string absolutePath,
        string displayName)
    {
        var document = new LinkedDocument
        {
            Id = Guid.CreateVersion7(),
            PromptId = prompt.Id,
            WorkingDirectoryId = prompt.WorkingDirectoryId,
            AbsolutePath = absolutePath,
            AbsolutePathKey = absolutePath.ToLowerInvariant(),
            DisplayName = displayName,
            Status = LinkedDocumentStatus.Tracking,
            CurrentVersion = 1
        };

        context.LinkedDocumentItems.Add(document);
        return document;
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
        public int SaveChangesCount { get; private set; }

        public IQueryable<User> Users => UserItems.AsQueryable();
        public IQueryable<WorkingDirectory> WorkingDirectories => WorkingDirectoryItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.FutureTasks.FutureTask> FutureTasks => Enumerable.Empty<PromptTasks.Domain.FutureTasks.FutureTask>().AsQueryable();
        public IQueryable<PromptTasks.Domain.FutureTasks.FutureTaskLabel> FutureTaskLabels => Enumerable.Empty<PromptTasks.Domain.FutureTasks.FutureTaskLabel>().AsQueryable();
        public IQueryable<Prompt> Prompts => PromptItems.AsQueryable();
        public IQueryable<PromptVersion> PromptVersions => PromptVersionItems.AsQueryable();
        public IQueryable<PromptFileReference> PromptFileReferences => PromptFileReferenceItems.AsQueryable();
        public IQueryable<LinkedDocument> LinkedDocuments => LinkedDocumentItems.AsQueryable();
        public IQueryable<LinkedDocumentVersion> LinkedDocumentVersions => LinkedDocumentVersionItems.AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.WorkflowTemplate> WorkflowTemplates => Enumerable.Empty<PromptTasks.Domain.Workflows.WorkflowTemplate>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.WorkflowTemplatePhase> WorkflowTemplatePhases => Enumerable.Empty<PromptTasks.Domain.Workflows.WorkflowTemplatePhase>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.PromptWorkflow> PromptWorkflows => Enumerable.Empty<PromptTasks.Domain.Workflows.PromptWorkflow>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.PromptWorkflowPhase> PromptWorkflowPhases => Enumerable.Empty<PromptTasks.Domain.Workflows.PromptWorkflowPhase>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Workflows.PromptWorkflowEvent> PromptWorkflowEvents => Enumerable.Empty<PromptTasks.Domain.Workflows.PromptWorkflowEvent>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<PromptTasks.Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<PromptTasks.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Note> Notes => Enumerable.Empty<PromptTasks.Domain.Notebooks.Note>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case Prompt prompt:
                    PromptItems.Add(prompt);
                    break;
                case PromptVersion version:
                    PromptVersionItems.Add(version);
                    break;
                case PromptFileReference reference:
                    PromptFileReferenceItems.Add(reference);
                    break;
                case LinkedDocument document:
                    LinkedDocumentItems.Add(document);
                    break;
                case LinkedDocumentVersion version:
                    LinkedDocumentVersionItems.Add(version);
                    break;
            }
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities)
            {
                Add(entity);
            }
        }

        public void Remove<TEntity>(TEntity entity) where TEntity : class
        {
            switch (entity)
            {
                case Prompt prompt:
                    PromptItems.Remove(prompt);
                    break;
                case PromptFileReference reference:
                    PromptFileReferenceItems.Remove(reference);
                    break;
                case LinkedDocument document:
                    LinkedDocumentItems.Remove(document);
                    break;
            }
        }

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
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }
}

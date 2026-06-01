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
                PromptTemplateKey.ReviewPullRequest);
        catalog.Get(PromptTemplateKey.ReviewPlan).Should().BeOfType<ReviewPlanTemplate>();
        catalog.Get(PromptTemplateKey.ImplementPlan).Should().BeOfType<ImplementPlanTemplate>();
        catalog.Get(PromptTemplateKey.ReviewPlanWithParentPrompt).Should().BeOfType<ReviewPlanWithParentPromptTemplate>();
        catalog.Get(PromptTemplateKey.ReReviewPlan).Should().BeOfType<ReReviewPlanTemplate>();
        catalog.Get(PromptTemplateKey.ImplementPlanInWorktree).Should().BeOfType<ImplementPlanInWorktreeTemplate>();
        catalog.Get(PromptTemplateKey.ReviewPullRequest).Should().BeOfType<ReviewPullRequestTemplate>();
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
        result.Title.Should().Be("Revisar plano: plan.md");
        result.Content.Should().Be(
            "Dado o plano \"C:/Users/psiel/.claude/plans/plan.md\", valide o plano, aprove-o ou aponte melhorias.");
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

        result.Title.Should().Be("Implementar plano: implementation.md");
        result.Content.Should().Be("Implemente o plano \"C:/plans/implementation.md\".");
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
        result.Title.Should().Be("Revisar plano com prompt pai: plan.md");
        result.Content.Should().Be(
            """
            Eu pedi para Claude fazer um plan-mode usando o prompt abaixo:

            ```md
            Faca um plano para @src/main.go
            ```

            Ele gerou o plano "C:/plans/plan.md".

            Dado o plano "C:/plans/plan.md", valide o plano, aprove-o ou aponte melhorias.
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
        result.Title.Should().Be("Re-review do plano: reviewed-plan.md");
        result.Content.Should().Be(
            "Eu passei os pontos anteriores para o Claude corrigir no plano \"C:/plans/reviewed-plan.md\". Valide novamente o plano atualizado, aprove-o se estiver correto ou aponte as melhorias que ainda faltam.");
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
        result.Title.Should().Be("Implementar em worktree: worktree-plan.md");
        result.Content.Should().Contain("Implemente o plano `C:/plans/worktree-plan.md` por completo em uma worktree separada.");
        result.Content.Should().Contain("crie uma PR");
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
        result.Title.Should().Be("Revisar PR #123: pr-plan.md");
        result.Content.Should().Contain("Revise a PR #123 que implementa o plano `C:/plans/pr-plan.md`.");
        result.Content.Should().Contain("Priorize bugs, riscos comportamentais e testes faltantes.");
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

    [Fact]
    public async Task GeneratePromptDraft_validation_requires_pr_for_review_pull_request()
    {
        var behavior = new ValidationBehavior<GeneratePromptDraftCommand, GeneratedPromptDraftDto>(
            new[] { new GeneratePromptDraftValidator() });
        var invalid = new GeneratePromptDraftCommand(Guid.CreateVersion7(), PromptTemplateKey.ReviewPullRequest);

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
            new ReviewPullRequestTemplate()
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

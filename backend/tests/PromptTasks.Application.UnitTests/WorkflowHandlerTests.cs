using FluentAssertions;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Workflow.Commands.AddReviewVerdict;
using PromptTasks.Application.Features.Workflow.Commands.AddWorkflowNote;
using PromptTasks.Application.Features.Workflow.Commands.AdvancePhase;
using PromptTasks.Application.Features.Workflow.Commands.ChangeActor;
using PromptTasks.Application.Features.Workflow.Commands.CompleteWorkflow;
using PromptTasks.Application.Features.Workflow.Commands.ReopenWorkflow;
using PromptTasks.Application.Features.Workflow.Commands.SetPhase;
using PromptTasks.Application.Features.Workflow.Commands.StartWorkflow;
using PromptTasks.Application.Features.Workflow.Commands.UpdateTaskPhases;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Users;
using PromptTasks.Domain.WorkingDirectories;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.UnitTests;

public sealed class WorkflowHandlerTests
{
    [Fact]
    public void WorkflowDefaults_defines_the_default_developer_pr_flow()
    {
        WorkflowDefaults.Phases.Select(phase => (phase.Name, phase.DefaultActor, phase.Role)).Should()
            .Equal(
                ("Engenharia de prompt", WorkflowActor.Human, WorkflowPhaseRole.PromptEngineering),
                ("Planejamento", WorkflowActor.ClaudeCode, WorkflowPhaseRole.Planning),
                ("Revisão do plano", WorkflowActor.Codex, WorkflowPhaseRole.PlanReview),
                ("Correção do plano", WorkflowActor.ClaudeCode, WorkflowPhaseRole.PlanCorrection),
                ("Implementação", WorkflowActor.Codex, WorkflowPhaseRole.Implementation),
                ("Revisão de código", WorkflowActor.ClaudeCode, WorkflowPhaseRole.CodeReview),
                ("Correção da revisão", WorkflowActor.Codex, WorkflowPhaseRole.ReviewCorrection),
                ("Teste prático", WorkflowActor.Human, WorkflowPhaseRole.PracticalTest),
                ("Atualizar branch com main", WorkflowActor.Codex, WorkflowPhaseRole.Rebase),
                ("Commit/Merge", WorkflowActor.Codex, WorkflowPhaseRole.Merge));
    }

    [Fact]
    public async Task StartWorkflow_copies_default_phases_and_records_started_event()
    {
        var fixture = new Fixture();

        var workflow = await fixture.StartAsync();

        workflow.Status.Should().Be(PromptWorkflowStatus.Active);
        workflow.Phases.Should().HaveCount(WorkflowDefaults.Phases.Count);
        workflow.Phases.Should().OnlyContain(phase =>
            WorkflowDefaults.Phases.Any(seed => seed.Name == phase.Name));
        workflow.CurrentPhaseName.Should().Be("Engenharia de prompt");
        workflow.CurrentActor.Should().Be(WorkflowActor.Human);
        workflow.CurrentPhaseIteration.Should().Be(1);
        workflow.Events.Should().ContainSingle(@event => @event.Type == WorkflowEventType.WorkflowStarted);
        fixture.Notifier.Changes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StartWorkflow_twice_conflicts()
    {
        var fixture = new Fixture();
        await fixture.StartAsync();

        var act = () => fixture.StartAsync();

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AdvancePhase_moves_to_the_next_phase()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();

        var advanced = await fixture.AdvanceAsync(started.RowVersion);

        advanced.CurrentPhaseName.Should().Be("Planejamento");
        advanced.CurrentActor.Should().Be(WorkflowActor.ClaudeCode);
        advanced.CurrentPhaseIteration.Should().Be(1);
        advanced.Events.Last().Type.Should().Be(WorkflowEventType.PhaseChanged);
    }

    [Fact]
    public async Task AdvancePhase_at_last_phase_completes_workflow()
    {
        var fixture = new Fixture();
        var workflow = await fixture.StartAsync();
        while (workflow.CurrentPhaseName != "Commit/Merge")
        {
            workflow = await fixture.AdvanceAsync(workflow.RowVersion);
        }

        var completed = await fixture.AdvanceAsync(workflow.RowVersion);

        completed.Status.Should().Be(PromptWorkflowStatus.Done);
        completed.CurrentPhaseName.Should().Be("Commit/Merge");
        completed.Events.Last().Type.Should().Be(WorkflowEventType.Completed);
    }

    [Fact]
    public async Task SetPhase_supports_the_review_fix_loop()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var atPlanning = await fixture.AdvanceAsync(started.RowVersion);
        var atReview = await fixture.AdvanceAsync(atPlanning.RowVersion);
        var atFix = await fixture.AdvanceAsync(atReview.RowVersion);
        atFix.CurrentPhaseName.Should().Be("Correção do plano");

        var reviewPhaseId = atFix.Phases.Single(phase => phase.Name == "Revisão do plano").Id;
        var backToReview = await fixture.SetPhaseAsync(reviewPhaseId, atFix.RowVersion);

        backToReview.CurrentPhaseName.Should().Be("Revisão do plano");
        backToReview.CurrentPhaseIteration.Should().Be(1);
        backToReview.Events.Count(@event => @event.Type == WorkflowEventType.PhaseChanged).Should().Be(4);
    }

    [Fact]
    public async Task ChangeActor_updates_current_actor()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();

        var changed = await fixture.ChangeActorAsync(WorkflowActor.Human, started.RowVersion);

        changed.CurrentActor.Should().Be(WorkflowActor.Human);
        changed.Events.Last().Type.Should().Be(WorkflowEventType.ActorChanged);
    }

    [Fact]
    public async Task AddNote_is_allowed_even_after_completion_and_needs_no_row_version()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var completed = await fixture.CompleteAsync(started.RowVersion);

        var withNote = await fixture.AddNoteAsync("Codex aprovou o plano");

        withNote.Status.Should().Be(PromptWorkflowStatus.Done);
        withNote.Events.Should().ContainSingle(@event => @event.Type == WorkflowEventType.Note && @event.Note == "Codex aprovou o plano");
        completed.Status.Should().Be(PromptWorkflowStatus.Done);
    }

    [Fact]
    public async Task Complete_then_reopen_returns_workflow_to_active()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var completed = await fixture.CompleteAsync(started.RowVersion);

        var reopened = await fixture.ReopenAsync(completed.RowVersion);

        reopened.Status.Should().Be(PromptWorkflowStatus.Active);
        reopened.Events.Last().Type.Should().Be(WorkflowEventType.Reopened);
    }

    [Fact]
    public async Task Complete_when_already_done_conflicts()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var completed = await fixture.CompleteAsync(started.RowVersion);

        var act = () => fixture.CompleteAsync(completed.RowVersion);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Advance_with_stale_row_version_conflicts()
    {
        var fixture = new Fixture();
        await fixture.StartAsync();

        var act = () => fixture.AdvanceAsync("999");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateTaskPhases_blocks_deleting_the_current_phase()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var remaining = started.Phases
            .Where(phase => phase.Id != started.CurrentPhaseId)
            .Select((phase, index) => new WorkflowPhaseInput(phase.Id, phase.Name, phase.DefaultActor, index, phase.Color))
            .ToList();

        var act = () => fixture.UpdatePhasesAsync(remaining, started.RowVersion);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateTaskPhases_blocks_deleting_a_phase_that_has_history()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var atPlanning = await fixture.AdvanceAsync(started.RowVersion);

        // Planning now has a PhaseChanged event and cannot be deleted.
        var planningId = atPlanning.Phases.Single(phase => phase.Name == "Planejamento").Id;
        var remaining = atPlanning.Phases
            .Where(phase => phase.Id != planningId)
            .Select((phase, index) => new WorkflowPhaseInput(phase.Id, phase.Name, phase.DefaultActor, index, phase.Color))
            .ToList();

        var act = () => fixture.UpdatePhasesAsync(remaining, atPlanning.RowVersion);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateTaskPhases_can_rename_and_append_a_phase()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var inputs = started.Phases
            .Select(phase => new WorkflowPhaseInput(
                phase.Id,
                phase.Name == "Commit/Merge" ? "Merge final" : phase.Name,
                phase.DefaultActor,
                phase.OrderIndex,
                phase.Color))
            .Append(new WorkflowPhaseInput(null, "Deploy", WorkflowActor.Human, started.Phases.Count, "#15803d"))
            .ToList();

        var updated = await fixture.UpdatePhasesAsync(inputs, started.RowVersion);

        updated.Phases.Should().HaveCount(WorkflowDefaults.Phases.Count + 1);
        updated.Phases.Should().Contain(phase => phase.Name == "Merge final");
        updated.Phases.Should().Contain(phase => phase.Name == "Deploy");
    }

    [Fact]
    public async Task AddReviewVerdict_in_plan_review_records_note_and_moves_to_plan_correction()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var planning = await fixture.AdvanceAsync(started.RowVersion);
        var review = await fixture.AdvanceAsync(planning.RowVersion);
        review.CurrentPhaseName.Should().Be("Revisão do plano");
        review.CurrentActor.Should().Be(WorkflowActor.Codex);

        var corrected = await fixture.AddReviewVerdictAsync("3 pontos a corrigir", review.RowVersion);

        corrected.CurrentPhaseName.Should().Be("Correção do plano");
        corrected.CurrentActor.Should().Be(WorkflowActor.ClaudeCode);
        corrected.ReviewVerdictSourcePhaseName.Should().Be("Revisão do plano");
        corrected.Events.Should().ContainSingle(@event =>
            @event.Type == WorkflowEventType.Note &&
            @event.Note == "3 pontos a corrigir" &&
            @event.Actor == WorkflowActor.Codex &&
            @event.PhaseName == "Revisão do plano");
        corrected.Events.Last().Type.Should().Be(WorkflowEventType.PhaseChanged);
        corrected.Events.Last().Note.Should().Contain("Revisão do plano");
    }

    [Fact]
    public void AddReviewVerdictValidator_allows_long_verdicts()
    {
        var validator = new AddReviewVerdictValidator();
        var verdict = new string('x', 5504);

        var result = validator.Validate(new AddReviewVerdictCommand(Guid.CreateVersion7(), verdict, "7"));

        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task AddReviewVerdict_in_code_review_moves_to_review_correction()
    {
        var fixture = new Fixture();
        var workflow = await fixture.StartAsync();
        while (workflow.CurrentPhaseName != "Revisão de código")
        {
            workflow = await fixture.AdvanceAsync(workflow.RowVersion);
        }

        workflow.CurrentActor.Should().Be(WorkflowActor.ClaudeCode);

        var corrected = await fixture.AddReviewVerdictAsync("PR tem regressões", workflow.RowVersion);

        corrected.CurrentPhaseName.Should().Be("Correção da revisão");
        corrected.CurrentActor.Should().Be(WorkflowActor.Codex);
        corrected.ReviewVerdictSourcePhaseName.Should().Be("Revisão de código");
        corrected.Events.Should().ContainSingle(@event =>
            @event.Type == WorkflowEventType.Note &&
            @event.Note == "PR tem regressões" &&
            @event.Actor == WorkflowActor.ClaudeCode);
    }

    [Fact]
    public async Task AddReviewVerdict_in_code_review_uses_custom_review_correction_phase_name()
    {
        const string customCorrectionPhase = "Correcao de Pontos da Revisao";
        var fixture = new Fixture();
        var workflow = await fixture.StartAsync();
        fixture.RenamePhaseAndClearRole("Corre\u00e7\u00e3o da revis\u00e3o", customCorrectionPhase);
        while (workflow.CurrentPhaseName != "Revis\u00e3o de c\u00f3digo")
        {
            workflow = await fixture.AdvanceAsync(workflow.RowVersion);
        }

        var corrected = await fixture.AddReviewVerdictAsync("PR tem pontos a corrigir", workflow.RowVersion);

        corrected.CurrentPhaseName.Should().Be(customCorrectionPhase);
        corrected.CurrentActor.Should().Be(WorkflowActor.Codex);
        corrected.Phases.Single(phase => phase.Name == customCorrectionPhase).Role.Should().Be(WorkflowPhaseRole.ReviewCorrection);
    }

    [Fact]
    public async Task AddReviewVerdict_outside_a_review_phase_conflicts()
    {
        var fixture = new Fixture();
        await fixture.StartAsync();

        var act = () => fixture.AddReviewVerdictAsync("qualquer", "0");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AddReviewVerdict_with_stale_row_version_conflicts()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var planning = await fixture.AdvanceAsync(started.RowVersion);
        await fixture.AdvanceAsync(planning.RowVersion);

        var act = () => fixture.AddReviewVerdictAsync("x", "999");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AddReviewVerdict_source_is_cleared_on_the_next_transition()
    {
        var fixture = new Fixture();
        var started = await fixture.StartAsync();
        var planning = await fixture.AdvanceAsync(started.RowVersion);
        var review = await fixture.AdvanceAsync(planning.RowVersion);
        var corrected = await fixture.AddReviewVerdictAsync("ajustar", review.RowVersion);
        corrected.ReviewVerdictSourcePhaseName.Should().Be("Revisão do plano");

        var advanced = await fixture.AdvanceAsync(corrected.RowVersion);

        advanced.CurrentPhaseName.Should().Be("Implementação");
        advanced.ReviewVerdictSourcePhaseName.Should().BeNull();
    }

    private sealed class Fixture
    {
        private readonly FakeWorkflowDbContext _context = new();
        private readonly FakeClock _clock = new();
        private readonly FakeCurrentUser _user = new();
        public FakeWorkflowNotifier Notifier { get; } = new();
        public Prompt Prompt { get; }

        public Fixture()
        {
            var workingDirectoryId = Guid.CreateVersion7();
            _context.WorkingDirectoryItems.Add(new WorkingDirectory
            {
                Id = workingDirectoryId,
                Name = "repo",
                AbsolutePath = "C:/repo",
                OwnerId = User.SystemUserId
            });
            Prompt = new Prompt
            {
                Id = Guid.CreateVersion7(),
                WorkingDirectoryId = workingDirectoryId,
                Title = "Tarefa",
                Content = "Conteúdo",
                OwnerId = User.SystemUserId,
                Status = PromptStatus.Draft
            };
            _context.PromptItems.Add(Prompt);
        }

        public Task<WorkflowDto> StartAsync() =>
            new StartWorkflowHandler(_context, Notifier, _user, _clock)
                .Handle(new StartWorkflowCommand(Prompt.Id, null), CancellationToken.None);

        public Task<WorkflowDto> AdvanceAsync(string rowVersion) =>
            new AdvancePhaseHandler(_context, Notifier, _user, _clock)
                .Handle(new AdvancePhaseCommand(Prompt.Id, rowVersion, null), CancellationToken.None);

        public Task<WorkflowDto> SetPhaseAsync(Guid phaseId, string rowVersion) =>
            new SetPhaseHandler(_context, Notifier, _user, _clock)
                .Handle(new SetPhaseCommand(Prompt.Id, phaseId, null, null, rowVersion), CancellationToken.None);

        public Task<WorkflowDto> ChangeActorAsync(WorkflowActor actor, string rowVersion) =>
            new ChangeActorHandler(_context, Notifier, _user, _clock)
                .Handle(new ChangeActorCommand(Prompt.Id, actor, null, rowVersion), CancellationToken.None);

        public Task<WorkflowDto> AddNoteAsync(string note) =>
            new AddWorkflowNoteHandler(_context, Notifier, _user, _clock)
                .Handle(new AddWorkflowNoteCommand(Prompt.Id, note), CancellationToken.None);

        public Task<WorkflowDto> AddReviewVerdictAsync(string verdict, string rowVersion) =>
            new AddReviewVerdictHandler(_context, Notifier, _user, _clock)
                .Handle(new AddReviewVerdictCommand(Prompt.Id, verdict, rowVersion), CancellationToken.None);

        public void RenamePhaseAndClearRole(string currentName, string newName)
        {
            var phase = _context.PromptWorkflowPhaseItems.Single(phase => phase.Name == currentName);
            phase.Name = newName;
            phase.Role = null;
        }

        public Task<WorkflowDto> CompleteAsync(string rowVersion) =>
            new CompleteWorkflowHandler(_context, Notifier, _user, _clock)
                .Handle(new CompleteWorkflowCommand(Prompt.Id, null, rowVersion), CancellationToken.None);

        public Task<WorkflowDto> ReopenAsync(string rowVersion) =>
            new ReopenWorkflowHandler(_context, Notifier, _user, _clock)
                .Handle(new ReopenWorkflowCommand(Prompt.Id, null, rowVersion), CancellationToken.None);

        public Task<WorkflowDto> UpdatePhasesAsync(IReadOnlyList<WorkflowPhaseInput> phases, string rowVersion) =>
            new UpdateTaskPhasesHandler(_context, Notifier, _user, _clock)
                .Handle(new UpdateTaskPhasesCommand(Prompt.Id, phases, rowVersion), CancellationToken.None);
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        private int _ticks;
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero).AddSeconds(_ticks++);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => User.SystemUserId;
    }

    private sealed class FakeWorkflowNotifier : IWorkflowNotifier
    {
        public List<TaskSummaryDto> Changes { get; } = new();

        public Task TaskWorkflowChangedAsync(TaskSummaryDto summary, CancellationToken cancellationToken)
        {
            Changes.Add(summary);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkflowDbContext : IApplicationDbContext
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
        public IQueryable<PromptTasks.Domain.Ai.AiChatSession> AiChatSessions => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatSession>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiChatMessage> AiChatMessages => Enumerable.Empty<PromptTasks.Domain.Ai.AiChatMessage>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Ai.AiUserSettings> AiUserSettings => Enumerable.Empty<PromptTasks.Domain.Ai.AiUserSettings>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Notebook> Notebooks => Enumerable.Empty<PromptTasks.Domain.Notebooks.Notebook>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Notebooks.Note> Notes => Enumerable.Empty<PromptTasks.Domain.Notebooks.Note>().AsQueryable();
        public IQueryable<PromptTasks.Domain.Diagrams.Diagram> Diagrams => Enumerable.Empty<PromptTasks.Domain.Diagrams.Diagram>().AsQueryable();

        public void Add<TEntity>(TEntity entity) where TEntity : class => Route(entity, add: true);

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities)
            {
                Route(entity, add: true);
            }
        }

        public void Remove<TEntity>(TEntity entity) where TEntity : class => Route(entity, add: false);

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            foreach (var entity in entities.ToList())
            {
                Route(entity, add: false);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        private void Route<TEntity>(TEntity entity, bool add) where TEntity : class
        {
            switch (entity)
            {
                case Prompt item: Apply(PromptItems, item, add); break;
                case PromptVersion item: Apply(PromptVersionItems, item, add); break;
                case PromptFileReference item: Apply(PromptFileReferenceItems, item, add); break;
                case WorkingDirectory item: Apply(WorkingDirectoryItems, item, add); break;
                case WorkflowTemplate item: Apply(WorkflowTemplateItems, item, add); break;
                case WorkflowTemplatePhase item: Apply(WorkflowTemplatePhaseItems, item, add); break;
                case PromptWorkflow item: Apply(PromptWorkflowItems, item, add); break;
                case PromptWorkflowPhase item: Apply(PromptWorkflowPhaseItems, item, add); break;
                case PromptWorkflowEvent item: Apply(PromptWorkflowEventItems, item, add); break;
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
}

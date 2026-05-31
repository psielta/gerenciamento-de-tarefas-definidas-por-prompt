using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.Workflow.Commands.AdvancePhase;

public sealed class AdvancePhaseHandler(
    IApplicationDbContext context,
    IWorkflowNotifier workflowNotifier,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AdvancePhaseCommand, WorkflowDto>
{
    public async Task<WorkflowDto> Handle(AdvancePhaseCommand request, CancellationToken cancellationToken)
    {
        var prompt = WorkflowMutationHelpers.GetOwnedPrompt(context, request.PromptId, currentUser.UserId);
        var workflow = context.PromptWorkflows.FirstOrDefault(item => item.PromptId == prompt.Id)
            ?? throw new NotFoundException("Workflow was not found.");
        WorkflowMutationHelpers.EnsureRowVersion(workflow, request.RowVersion);
        WorkflowMutationHelpers.EnsureActive(workflow);

        var phases = WorkflowMutationHelpers.LoadPhases(context, workflow.Id);
        var current = phases.FirstOrDefault(phase => phase.Id == workflow.CurrentPhaseId);
        var currentOrder = current?.OrderIndex ?? -1;
        var now = dateTimeProvider.UtcNow;
        var next = phases.Where(phase => phase.OrderIndex > currentOrder).OrderBy(phase => phase.OrderIndex).FirstOrDefault();
        if (next is null)
        {
            workflow.Status = PromptWorkflowStatus.Done;
            workflow.UpdatedAtUtc = now;
            WorkflowMutationHelpers.AppendEvent(
                context,
                workflow,
                WorkflowEventType.Completed,
                current,
                workflow.CurrentActor,
                NormalizeNote(request.Note),
                now);
        }
        else
        {
            WorkflowMutationHelpers.EnterPhase(workflow, next, next.DefaultActor, now);
            WorkflowMutationHelpers.AppendEvent(
                context, workflow, WorkflowEventType.PhaseChanged, next, next.DefaultActor, NormalizeNote(request.Note), now);
        }

        await context.SaveChangesAsync(cancellationToken);

        var events = WorkflowMutationHelpers.LoadEvents(context, workflow.Id);
        var dto = workflow.ToDto(phases, events);
        await workflowNotifier.TaskWorkflowChangedAsync(TaskSummaryFactory.Build(context, prompt, workflow), cancellationToken);
        return dto;
    }

    private static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}

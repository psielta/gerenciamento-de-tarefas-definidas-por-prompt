using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.Workflow.Commands.StartWorkflow;

public sealed class StartWorkflowHandler(
    IApplicationDbContext context,
    IWorkflowNotifier workflowNotifier,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<StartWorkflowCommand, WorkflowDto>
{
    public async Task<WorkflowDto> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
        var prompt = WorkflowMutationHelpers.GetOwnedPrompt(context, request.PromptId, currentUser.UserId);

        if (context.PromptWorkflows.Any(item => item.PromptId == prompt.Id))
        {
            throw new ConflictException("A workflow was already started for this prompt.");
        }

        var (_, templatePhases, _) = WorkflowTemplateHelpers.ResolveOrCreate(context, currentUser.UserId);
        if (templatePhases.Count == 0)
        {
            throw new ConflictException("The workflow template has no phases.");
        }

        var now = dateTimeProvider.UtcNow;
        var workflow = new PromptWorkflow
        {
            PromptId = prompt.Id,
            Status = PromptWorkflowStatus.Active,
            StartedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var snapshot = new List<PromptWorkflowPhase>();
        var orderIndex = 0;
        foreach (var templatePhase in templatePhases.OrderBy(phase => phase.OrderIndex))
        {
            snapshot.Add(new PromptWorkflowPhase
            {
                PromptWorkflowId = workflow.Id,
                Name = templatePhase.Name,
                DefaultActor = templatePhase.DefaultActor,
                OrderIndex = orderIndex++,
                Color = templatePhase.Color,
                Role = templatePhase.Role
            });
        }

        var initialIndex = Math.Clamp(request.InitialPhaseOrderIndex ?? 0, 0, snapshot.Count - 1);
        var initialPhase = snapshot[initialIndex];
        WorkflowMutationHelpers.EnterPhase(workflow, initialPhase, initialPhase.DefaultActor, now);

        context.Add(workflow);
        foreach (var phase in snapshot)
        {
            context.Add(phase);
        }

        var startedEvent = WorkflowMutationHelpers.AppendEvent(
            context,
            workflow,
            WorkflowEventType.WorkflowStarted,
            initialPhase,
            initialPhase.DefaultActor,
            null,
            now);

        await context.SaveChangesAsync(cancellationToken);

        var dto = workflow.ToDto(snapshot, new[] { startedEvent });
        await workflowNotifier.TaskWorkflowChangedAsync(TaskSummaryFactory.Build(context, prompt, workflow), cancellationToken);
        return dto;
    }
}

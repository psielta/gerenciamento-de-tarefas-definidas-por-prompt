using System.Globalization;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Common.Mappings;

public static class WorkflowDtoMapper
{
    public static WorkflowPhaseDto ToDto(this PromptWorkflowPhase phase) =>
        new(phase.Id, phase.Name, phase.DefaultActor, phase.OrderIndex, phase.Color);

    public static WorkflowPhaseDto ToDto(this WorkflowTemplatePhase phase) =>
        new(phase.Id, phase.Name, phase.DefaultActor, phase.OrderIndex, phase.Color);

    public static WorkflowEventDto ToDto(this PromptWorkflowEvent @event) =>
        new(
            @event.Id,
            @event.Type,
            @event.PhaseId,
            @event.PhaseNameSnapshot,
            @event.Actor,
            @event.Note,
            @event.OccurredAtUtc);

    public static WorkflowDto ToDto(
        this PromptWorkflow workflow,
        IEnumerable<PromptWorkflowPhase> phases,
        IEnumerable<PromptWorkflowEvent> events) =>
        new(
            workflow.Id,
            workflow.PromptId,
            workflow.Status,
            workflow.CurrentPhaseId,
            workflow.CurrentPhaseName,
            workflow.CurrentPhaseColor,
            workflow.CurrentActor,
            workflow.StartedAtUtc,
            workflow.EnteredCurrentPhaseAtUtc,
            workflow.CurrentPhaseIteration,
            workflow.UpdatedAtUtc,
            workflow.RowVersion.ToString(CultureInfo.InvariantCulture),
            phases.OrderBy(phase => phase.OrderIndex).Select(phase => phase.ToDto()).ToList(),
            events.OrderBy(@event => @event.OccurredAtUtc).Select(@event => @event.ToDto()).ToList());

    public static WorkflowDto ToDto(this PromptWorkflow workflow) =>
        workflow.ToDto(workflow.Phases, workflow.Events);

    public static WorkflowTemplateDto ToDto(this WorkflowTemplate template) =>
        new(
            template.Id,
            template.Name,
            template.Phases.OrderBy(phase => phase.OrderIndex).Select(phase => phase.ToDto()).ToList());
}

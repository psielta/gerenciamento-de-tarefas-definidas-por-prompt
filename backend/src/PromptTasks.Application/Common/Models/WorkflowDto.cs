using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Common.Models;

public sealed record WorkflowDto(
    Guid Id,
    Guid PromptId,
    PromptWorkflowStatus Status,
    Guid? CurrentPhaseId,
    string? CurrentPhaseName,
    string? CurrentPhaseColor,
    WorkflowActor? CurrentActor,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EnteredCurrentPhaseAtUtc,
    int CurrentPhaseIteration,
    DateTimeOffset UpdatedAtUtc,
    string RowVersion,
    IReadOnlyList<WorkflowPhaseDto> Phases,
    IReadOnlyList<WorkflowEventDto> Events);

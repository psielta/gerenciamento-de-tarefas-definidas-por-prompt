using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Common.Models;

public sealed record TaskSummaryDto(
    Guid PromptId,
    Guid WorkingDirectoryId,
    string WorkingDirectoryName,
    string Title,
    PromptStatus PromptStatus,
    PromptWorkflowStatus? WorkflowStatus,
    Guid? CurrentPhaseId,
    string? CurrentPhaseName,
    string? CurrentPhaseColor,
    WorkflowActor? CurrentActor,
    DateTimeOffset? EnteredCurrentPhaseAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool HasChildPrompts,
    bool HasLinkedPlan,
    string PromptRowVersion,
    IReadOnlyList<WorkflowPhaseDto> Phases,
    string? RowVersion);

using System.Globalization;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.Workflow;

internal static class TaskSummaryFactory
{
    public static TaskSummaryDto Build(IApplicationDbContext context, Prompt prompt, PromptWorkflow? workflow)
    {
        var workingDirectoryName = context.WorkingDirectories
            .Where(directory => directory.Id == prompt.WorkingDirectoryId)
            .Select(directory => directory.Name)
            .FirstOrDefault() ?? string.Empty;
        var hasChildPrompts = context.Prompts.Any(item => item.ParentPromptId == prompt.Id);
        var hasLinkedPlan = context.LinkedDocuments.Any(document => document.PromptId == prompt.Id);
        var updatedAtUtc = workflow is not null && workflow.UpdatedAtUtc > prompt.UpdatedAtUtc
            ? workflow.UpdatedAtUtc
            : prompt.UpdatedAtUtc;
        IReadOnlyList<WorkflowPhaseDto> phases = workflow is null
            ? Array.Empty<WorkflowPhaseDto>()
            : context.PromptWorkflowPhases
                .Where(phase => phase.PromptWorkflowId == workflow.Id)
                .OrderBy(phase => phase.OrderIndex)
                .ToList()
                .Select(phase => phase.ToDto())
                .ToList();

        return new TaskSummaryDto(
            prompt.Id,
            prompt.WorkingDirectoryId,
            workingDirectoryName,
            prompt.TaskNumber,
            prompt.Title,
            prompt.Status,
            workflow?.Status,
            workflow?.CurrentPhaseId,
            workflow?.CurrentPhaseName,
            workflow?.CurrentPhaseColor,
            workflow?.CurrentActor,
            workflow?.EnteredCurrentPhaseAtUtc,
            workflow?.CurrentPhaseIteration ?? 1,
            updatedAtUtc,
            hasChildPrompts,
            hasLinkedPlan,
            prompt.RowVersion.ToString(CultureInfo.InvariantCulture),
            phases,
            workflow is null ? null : workflow.RowVersion.ToString(CultureInfo.InvariantCulture));
    }
}

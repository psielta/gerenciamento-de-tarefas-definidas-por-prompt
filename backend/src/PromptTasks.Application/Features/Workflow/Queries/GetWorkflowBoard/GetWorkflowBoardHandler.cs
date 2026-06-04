using System.Globalization;
using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.Workflow.Queries.GetWorkflowBoard;

public sealed class GetWorkflowBoardHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetWorkflowBoardQuery, IReadOnlyList<TaskSummaryDto>>
{
    public Task<IReadOnlyList<TaskSummaryDto>> Handle(GetWorkflowBoardQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var promptsQuery = context.Prompts.Where(prompt => prompt.OwnerId == userId && prompt.ParentPromptId == null);

        if (request.WorkingDirectoryId is { } workingDirectoryId)
        {
            promptsQuery = promptsQuery.Where(prompt => prompt.WorkingDirectoryId == workingDirectoryId);
        }

        if (request.PromptStatus is { } promptStatus)
        {
            promptsQuery = promptsQuery.Where(prompt => prompt.Status == promptStatus);
        }
        else
        {
            promptsQuery = promptsQuery.Where(prompt => prompt.Status != PromptStatus.Archived);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var term = request.Q.Trim();
            promptsQuery = promptsQuery.Where(prompt =>
                prompt.Title.Contains(term) ||
                prompt.Content.Contains(term) ||
                (prompt.TaskNumber != null && prompt.TaskNumber.Contains(term)));
        }

        var prompts = promptsQuery.ToList();
        var promptIds = prompts.Select(prompt => prompt.Id).ToHashSet();

        var workflows = context.PromptWorkflows
            .Where(workflow => promptIds.Contains(workflow.PromptId))
            .ToList()
            .ToDictionary(workflow => workflow.PromptId);
        var workflowIds = workflows.Values.Select(workflow => workflow.Id).ToHashSet();
        var phasesByWorkflowId = context.PromptWorkflowPhases
            .Where(phase => workflowIds.Contains(phase.PromptWorkflowId))
            .ToList()
            .GroupBy(phase => phase.PromptWorkflowId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(phase => phase.OrderIndex)
                    .Select(phase => phase.ToDto())
                    .ToList());

        var workingDirectoryIds = prompts.Select(prompt => prompt.WorkingDirectoryId).ToHashSet();
        var workingDirectoryNames = context.WorkingDirectories
            .Where(directory => workingDirectoryIds.Contains(directory.Id))
            .ToList()
            .ToDictionary(directory => directory.Id, directory => directory.Name);

        var promptsWithChildren = context.Prompts
            .Where(prompt => prompt.ParentPromptId != null && promptIds.Contains(prompt.ParentPromptId!.Value))
            .Select(prompt => prompt.ParentPromptId!.Value)
            .Distinct()
            .ToList()
            .ToHashSet();

        var promptsWithLinkedPlan = context.LinkedDocuments
            .Where(document => promptIds.Contains(document.PromptId))
            .Select(document => document.PromptId)
            .Distinct()
            .ToList()
            .ToHashSet();

        var result = new List<TaskSummaryDto>();
        foreach (var prompt in prompts)
        {
            workflows.TryGetValue(prompt.Id, out var workflow);

            if (request.WorkflowStatus is { } workflowStatus && (workflow is null || workflow.Status != workflowStatus))
            {
                continue;
            }

            var updatedAtUtc = workflow is not null && workflow.UpdatedAtUtc > prompt.UpdatedAtUtc
                ? workflow.UpdatedAtUtc
                : prompt.UpdatedAtUtc;

            result.Add(new TaskSummaryDto(
                prompt.Id,
                prompt.WorkingDirectoryId,
                workingDirectoryNames.GetValueOrDefault(prompt.WorkingDirectoryId, string.Empty),
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
                promptsWithChildren.Contains(prompt.Id),
                promptsWithLinkedPlan.Contains(prompt.Id),
                prompt.RowVersion.ToString(CultureInfo.InvariantCulture),
                workflow is null || !phasesByWorkflowId.TryGetValue(workflow.Id, out var phases)
                    ? Array.Empty<WorkflowPhaseDto>()
                    : phases,
                workflow is null ? null : workflow.RowVersion.ToString(CultureInfo.InvariantCulture)));
        }

        IReadOnlyList<TaskSummaryDto> ordered = result
            .OrderByDescending(summary => summary.UpdatedAtUtc)
            .ToList();
        return Task.FromResult(ordered);
    }
}

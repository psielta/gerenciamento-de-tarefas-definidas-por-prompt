using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Prompts;
using PromptTasks.Application.Features.Workflow;
using PromptTasks.Domain.Prompts;
using PromptTasks.Domain.Workflows;

namespace PromptTasks.Application.Features.Prompts.Commands.CreatePrompt;

public sealed class CreatePromptHandler(
    IApplicationDbContext context,
    IWorkspaceFileService workspaceFileService,
    IPromptNotifier promptNotifier,
    IWorkflowNotifier workflowNotifier,
    IDailyTaskSequenceProvider dailyTaskSequenceProvider,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreatePromptCommand, PromptDto>
{
    public async Task<PromptDto> Handle(CreatePromptCommand request, CancellationToken cancellationToken)
    {
        var directory = PromptMutationHelpers.GetWorkingDirectory(context, request.WorkingDirectoryId, currentUser.UserId);
        var parentPrompt = request.ParentPromptId.HasValue
            ? PromptMutationHelpers.GetPrompt(context, request.ParentPromptId.Value, currentUser.UserId)
            : null;
        if (parentPrompt is not null && parentPrompt.WorkingDirectoryId != directory.Id)
        {
            throw new ConflictException("Child prompts must use the same working directory as the parent prompt.");
        }

        var prompt = new Prompt
        {
            WorkingDirectoryId = directory.Id,
            ParentPromptId = parentPrompt?.Id,
            Title = request.Title.Trim(),
            Content = request.Content,
            TargetAgent = request.TargetAgent,
            Kind = request.Kind,
            Status = request.Status,
            CurrentVersion = 1,
            OwnerId = currentUser.UserId
        };

        if (prompt.ParentPromptId is null && !string.IsNullOrWhiteSpace(directory.TaskNumberPattern))
        {
            var sequenceDate = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
            var sequence = await dailyTaskSequenceProvider.NextAsync(directory.Id, sequenceDate, cancellationToken);
            prompt.TaskNumber = TaskNumberFormatter.Format(directory.TaskNumberPattern, sequence, sequenceDate);
        }

        var references = await PromptMutationHelpers.BuildReferencesAsync(
            workspaceFileService,
            directory.AbsolutePath,
            request.Mentions,
            cancellationToken);

        foreach (var reference in references)
        {
            reference.PromptId = prompt.Id;
        }

        context.Add(prompt);
        context.Add(PromptMutationHelpers.CreateVersion(prompt, dateTimeProvider, "Created"));
        context.AddRange(references);

        // Root prompts are tasks: start their workflow atomically so a task always has a timeline.
        var workflow = TryStartWorkflow(prompt);

        await context.SaveChangesAsync(cancellationToken);

        var dto = prompt.ToDto(references);
        await promptNotifier.PromptCreatedAsync(dto, cancellationToken);
        if (workflow is not null)
        {
            await workflowNotifier.TaskWorkflowChangedAsync(
                TaskSummaryFactory.Build(context, prompt, workflow),
                cancellationToken);
        }

        return dto;
    }

    private PromptWorkflow? TryStartWorkflow(Prompt prompt)
    {
        if (prompt.ParentPromptId is not null || prompt.Status == PromptStatus.Archived)
        {
            return null;
        }

        var (_, templatePhases, _) = WorkflowTemplateHelpers.ResolveOrCreate(context, currentUser.UserId);
        if (templatePhases.Count == 0)
        {
            return null;
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

        var orderIndex = 0;
        var snapshot = new List<PromptWorkflowPhase>();
        foreach (var templatePhase in templatePhases.OrderBy(phase => phase.OrderIndex))
        {
            snapshot.Add(new PromptWorkflowPhase
            {
                PromptWorkflowId = workflow.Id,
                Name = templatePhase.Name,
                DefaultActor = templatePhase.DefaultActor,
                OrderIndex = orderIndex++,
                Color = templatePhase.Color
            });
        }

        var initialPhase = snapshot[0];
        WorkflowMutationHelpers.EnterPhase(workflow, initialPhase, initialPhase.DefaultActor, now);

        context.Add(workflow);
        foreach (var phase in snapshot)
        {
            context.Add(phase);
        }

        WorkflowMutationHelpers.AppendEvent(
            context, workflow, WorkflowEventType.WorkflowStarted, initialPhase, initialPhase.DefaultActor, null, now);

        return workflow;
    }
}

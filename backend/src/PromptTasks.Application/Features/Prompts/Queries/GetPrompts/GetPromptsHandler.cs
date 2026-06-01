using MediatR;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Prompts.Queries.GetPrompts;

public sealed class GetPromptsHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetPromptsQuery, IReadOnlyList<PromptDto>>
{
    public Task<IReadOnlyList<PromptDto>> Handle(GetPromptsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Prompts.Where(prompt => prompt.OwnerId == currentUser.UserId);

        if (request.WorkingDirectoryId is { } workingDirectoryId)
        {
            query = query.Where(prompt => prompt.WorkingDirectoryId == workingDirectoryId);
        }

        if (request.ParentPromptId is { } parentPromptId)
        {
            query = query.Where(prompt => prompt.ParentPromptId == parentPromptId);
        }
        else if (request.RootOnly)
        {
            query = query.Where(prompt => prompt.ParentPromptId == null);
        }

        if (request.Status is { } status)
        {
            query = query.Where(prompt => prompt.Status == status);
        }

        if (request.Agent is { } agent)
        {
            query = query.Where(prompt => prompt.TargetAgent == agent);
        }

        if (request.Kind is { } kind)
        {
            query = query.Where(prompt => prompt.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var term = request.Q.Trim();
            query = query.Where(prompt =>
                prompt.Title.Contains(term) ||
                prompt.Content.Contains(term) ||
                (prompt.TaskNumber != null && prompt.TaskNumber.Contains(term)));
        }

        var prompts = query
            .OrderByDescending(prompt => prompt.UpdatedAtUtc)
            .ThenBy(prompt => prompt.Title)
            .ToList();

        var promptIds = prompts.Select(prompt => prompt.Id).ToHashSet();
        var references = context.PromptFileReferences
            .Where(reference => promptIds.Contains(reference.PromptId))
            .ToList()
            .GroupBy(reference => reference.PromptId)
            .ToDictionary(group => group.Key, group => group.AsEnumerable());

        IReadOnlyList<PromptDto> result = prompts
            .Select(prompt => prompt.ToDto(references.GetValueOrDefault(prompt.Id) ?? Array.Empty<Domain.Prompts.PromptFileReference>()))
            .ToList();

        return Task.FromResult(result);
    }
}

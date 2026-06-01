using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Mappings;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Prompts.Queries.GetPromptByTaskNumber;

public sealed class GetPromptByTaskNumberHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetPromptByTaskNumberQuery, PromptDto>
{
    public Task<PromptDto> Handle(GetPromptByTaskNumberQuery request, CancellationToken cancellationToken)
    {
        var prompt = context.Prompts.FirstOrDefault(item =>
            item.OwnerId == currentUser.UserId &&
            item.WorkingDirectoryId == request.WorkingDirectoryId &&
            item.ParentPromptId == null &&
            item.TaskNumber == request.TaskNumber);

        if (prompt is null)
        {
            throw new NotFoundException("Prompt was not found.");
        }

        var references = context.PromptFileReferences
            .Where(reference => reference.PromptId == prompt.Id)
            .ToList();

        return Task.FromResult(prompt.ToDto(references));
    }
}

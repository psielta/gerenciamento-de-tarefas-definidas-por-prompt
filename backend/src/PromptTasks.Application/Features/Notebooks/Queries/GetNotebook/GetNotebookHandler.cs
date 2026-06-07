using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Queries.GetNotebook;

public sealed class GetNotebookHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<GetNotebookQuery, NotebookDto>
{
    public Task<NotebookDto> Handle(GetNotebookQuery request, CancellationToken cancellationToken)
    {
        var notebook = context.Notebooks
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (notebook is null)
        {
            throw new NotFoundException("Notebook was not found.");
        }

        return Task.FromResult(notebook.ToDtoWithDetails(context));
    }
}

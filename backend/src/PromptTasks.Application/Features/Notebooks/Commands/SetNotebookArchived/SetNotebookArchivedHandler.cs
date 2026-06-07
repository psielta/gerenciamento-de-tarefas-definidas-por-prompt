using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Commands.SetNotebookArchived;

public sealed class SetNotebookArchivedHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<SetNotebookArchivedCommand, NotebookDto>
{
    public async Task<NotebookDto> Handle(SetNotebookArchivedCommand request, CancellationToken cancellationToken)
    {
        var notebook = context.Notebooks
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (notebook is null)
        {
            throw new NotFoundException("Notebook was not found.");
        }

        notebook.IsArchived = request.IsArchived;
        await context.SaveChangesAsync(cancellationToken);

        return notebook.ToDtoWithDetails(context);
    }
}

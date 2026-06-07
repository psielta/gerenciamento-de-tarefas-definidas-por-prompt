using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;

namespace PromptTasks.Application.Features.Notebooks.Commands.UpdateNotebook;

public sealed class UpdateNotebookHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<UpdateNotebookCommand, NotebookDto>
{
    public async Task<NotebookDto> Handle(UpdateNotebookCommand request, CancellationToken cancellationToken)
    {
        var notebook = context.Notebooks
            .FirstOrDefault(item => item.Id == request.Id && item.OwnerId == currentUser.UserId);

        if (notebook is null)
        {
            throw new NotFoundException("Notebook was not found.");
        }

        if (request.WorkingDirectoryId is { } workingDirectoryId)
        {
            var owned = context.WorkingDirectories
                .Any(directory => directory.Id == workingDirectoryId && directory.OwnerId == currentUser.UserId);
            if (!owned)
            {
                throw new NotFoundException("Working directory was not found.");
            }
        }

        notebook.Title = request.Title.Trim();
        notebook.Description = Normalize(request.Description);
        notebook.WorkingDirectoryId = request.WorkingDirectoryId;

        await context.SaveChangesAsync(cancellationToken);

        return notebook.ToDtoWithDetails(context);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

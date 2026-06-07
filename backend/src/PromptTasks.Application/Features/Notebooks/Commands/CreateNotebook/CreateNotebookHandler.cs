using MediatR;
using PromptTasks.Application.Common.Exceptions;
using PromptTasks.Application.Common.Interfaces;
using PromptTasks.Application.Common.Models;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Application.Features.Notebooks.Commands.CreateNotebook;

public sealed class CreateNotebookHandler(IApplicationDbContext context, ICurrentUser currentUser)
    : IRequestHandler<CreateNotebookCommand, NotebookDto>
{
    public async Task<NotebookDto> Handle(CreateNotebookCommand request, CancellationToken cancellationToken)
    {
        if (request.WorkingDirectoryId is { } workingDirectoryId)
        {
            var owned = context.WorkingDirectories
                .Any(directory => directory.Id == workingDirectoryId && directory.OwnerId == currentUser.UserId);
            if (!owned)
            {
                throw new NotFoundException("Working directory was not found.");
            }
        }

        var notebook = new Notebook
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            WorkingDirectoryId = request.WorkingDirectoryId,
            OwnerId = currentUser.UserId
        };

        context.Add(notebook);
        await context.SaveChangesAsync(cancellationToken);

        return notebook.ToDtoWithDetails(context);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

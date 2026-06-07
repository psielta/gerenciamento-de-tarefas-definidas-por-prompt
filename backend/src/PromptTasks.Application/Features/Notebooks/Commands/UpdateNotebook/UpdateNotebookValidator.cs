using FluentValidation;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Application.Features.Notebooks.Commands.UpdateNotebook;

public sealed class UpdateNotebookValidator : AbstractValidator<UpdateNotebookCommand>
{
    public UpdateNotebookValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Notebook.MaxTitleLength);
        RuleFor(command => command.Description).MaximumLength(Notebook.MaxDescriptionLength);
    }
}

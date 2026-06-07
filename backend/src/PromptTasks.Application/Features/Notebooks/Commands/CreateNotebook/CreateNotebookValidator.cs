using FluentValidation;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Application.Features.Notebooks.Commands.CreateNotebook;

public sealed class CreateNotebookValidator : AbstractValidator<CreateNotebookCommand>
{
    public CreateNotebookValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Notebook.MaxTitleLength);
        RuleFor(command => command.Description).MaximumLength(Notebook.MaxDescriptionLength);
    }
}

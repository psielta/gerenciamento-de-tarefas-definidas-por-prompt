using FluentValidation;
using PromptTasks.Domain.Notebooks;

namespace PromptTasks.Application.Features.Notes.Commands.UpdateNote;

public sealed class UpdateNoteValidator : AbstractValidator<UpdateNoteCommand>
{
    public UpdateNoteValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(Note.MaxTitleLength);
        RuleFor(command => command.ContentMarkdown).NotNull().MaximumLength(Note.MaxContentLength);
    }
}

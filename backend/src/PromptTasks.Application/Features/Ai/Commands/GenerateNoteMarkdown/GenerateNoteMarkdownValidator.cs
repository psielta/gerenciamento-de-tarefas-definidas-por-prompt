using FluentValidation;

namespace PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;

public sealed class GenerateNoteMarkdownValidator : AbstractValidator<GenerateNoteMarkdownCommand>
{
    public GenerateNoteMarkdownValidator()
    {
        RuleFor(c => c.Instruction).NotEmpty().MaximumLength(4_000);
        RuleFor(c => c.Model).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Temperature).InclusiveBetween(0.0, 2.0);
        RuleFor(c => c.CurrentContent)
            .MaximumLength(100_000)
            .When(c => c.CurrentContent is not null);
        RuleFor(c => c.Format)
            .Must(NoteFormats.IsKnown)
            .When(c => !string.IsNullOrWhiteSpace(c.Format))
            .WithMessage("Formato de nota invalido.");
    }
}

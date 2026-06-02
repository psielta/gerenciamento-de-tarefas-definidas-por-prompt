using FluentValidation;

namespace PromptTasks.Application.Features.Ai.Commands.TranslatePrompt;

public sealed class TranslatePromptValidator : AbstractValidator<TranslatePromptCommand>
{
    public TranslatePromptValidator()
    {
        RuleFor(c => c.Content).NotEmpty().MaximumLength(200_000);
        RuleFor(c => c.Model).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Temperature).InclusiveBetween(0.0, 2.0);
    }
}

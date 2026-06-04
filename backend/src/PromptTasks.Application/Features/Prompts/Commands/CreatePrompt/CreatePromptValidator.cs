using FluentValidation;

namespace PromptTasks.Application.Features.Prompts.Commands.CreatePrompt;

public sealed class CreatePromptValidator : AbstractValidator<CreatePromptCommand>
{
    public CreatePromptValidator()
    {
        RuleFor(command => command.WorkingDirectoryId).NotEmpty();
        RuleFor(command => command.ParentPromptId)
            .NotEqual(Guid.Empty)
            .When(command => command.ParentPromptId.HasValue);
        RuleFor(command => command.Title).NotEmpty().MaximumLength(220);
        RuleFor(command => command.Content).NotNull().MaximumLength(200_000);
        RuleFor(command => command.TargetAgent).IsInEnum();
        RuleFor(command => command.Kind).IsInEnum();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.SourceTemplateKey)
            .IsInEnum()
            .When(command => command.SourceTemplateKey.HasValue);
        RuleForEach(command => command.Mentions).ChildRules(mention =>
        {
            mention.RuleFor(item => item.Id).NotEmpty().MaximumLength(1024);
            mention.RuleFor(item => item.Label).MaximumLength(1024);
        });
    }
}

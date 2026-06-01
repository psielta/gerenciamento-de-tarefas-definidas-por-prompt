using FluentValidation;
using PromptTasks.Application.Features.Prompts;

namespace PromptTasks.Application.Features.WorkingDirectories.Commands.CreateWorkingDirectory;

public sealed class CreateWorkingDirectoryValidator : AbstractValidator<CreateWorkingDirectoryCommand>
{
    public CreateWorkingDirectoryValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(160);
        RuleFor(command => command.AbsolutePath).NotEmpty().MaximumLength(1024);
        RuleFor(command => command.TaskNumberPattern)
            .MaximumLength(TaskNumberFormatter.MaxPatternLength)
            .Custom((pattern, context) =>
            {
                foreach (var error in TaskNumberFormatter.Validate(pattern))
                {
                    context.AddFailure(error);
                }
            });
    }
}

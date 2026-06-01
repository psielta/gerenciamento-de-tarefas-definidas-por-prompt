using FluentValidation;

namespace PromptTasks.Application.Features.Prompts.Queries.GetPromptByTaskNumber;

public sealed class GetPromptByTaskNumberValidator : AbstractValidator<GetPromptByTaskNumberQuery>
{
    public GetPromptByTaskNumberValidator()
    {
        RuleFor(query => query.WorkingDirectoryId).NotEmpty();
        RuleFor(query => query.TaskNumber).NotEmpty().MaximumLength(64);
    }
}

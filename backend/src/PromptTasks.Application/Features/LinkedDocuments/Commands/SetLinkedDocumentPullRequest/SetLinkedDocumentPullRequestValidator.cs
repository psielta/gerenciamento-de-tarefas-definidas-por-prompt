using FluentValidation;

namespace PromptTasks.Application.Features.LinkedDocuments.Commands.SetLinkedDocumentPullRequest;

public sealed class SetLinkedDocumentPullRequestValidator : AbstractValidator<SetLinkedDocumentPullRequestCommand>
{
    public SetLinkedDocumentPullRequestValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.PullRequest).MaximumLength(120);
    }
}

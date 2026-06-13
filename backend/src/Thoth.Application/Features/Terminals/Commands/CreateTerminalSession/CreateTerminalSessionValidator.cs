using FluentValidation;

namespace Thoth.Application.Features.Terminals.Commands.CreateTerminalSession;

public sealed class CreateTerminalSessionValidator : AbstractValidator<CreateTerminalSessionCommand>
{
    public CreateTerminalSessionValidator()
    {
        RuleFor(command => command.PromptId).NotEmpty();
        RuleFor(command => command.Shell)
            .MaximumLength(260)
            .When(command => !string.IsNullOrWhiteSpace(command.Shell));
        RuleFor(command => command.AgentLaunch)
            .IsInEnum()
            .When(command => command.AgentLaunch.HasValue);
    }
}
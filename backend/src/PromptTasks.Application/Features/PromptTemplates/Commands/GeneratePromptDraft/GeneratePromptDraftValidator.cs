using FluentValidation;
using PromptTasks.Domain.Prompts;

namespace PromptTasks.Application.Features.PromptTemplates.Commands.GeneratePromptDraft;

public sealed class GeneratePromptDraftValidator : AbstractValidator<GeneratePromptDraftCommand>
{
    public GeneratePromptDraftValidator()
    {
        RuleFor(command => command.LinkedDocumentId).NotEmpty();
        RuleFor(command => command.TemplateKey).IsInEnum();
        // Obrigatoriedade da PR e validada no handler, apos resolver o fallback do plano vinculado
        // (GeneratePromptDraftHandler). Aqui mantemos apenas o limite de tamanho do valor informado.
        RuleFor(command => GetInputValue(command, "pullRequest"))
            .MaximumLength(120);
        When(
            command => command.TemplateKey is PromptTemplateKey.ReReviewPullRequest,
            () =>
            {
                RuleFor(command => GetInputValue(command, "codexResponse"))
                    .NotEmpty()
                    .MaximumLength(20_000);
            });
    }

    private static string? GetInputValue(GeneratePromptDraftCommand command, string key)
    {
        if (command.Inputs is not null)
        {
            var input = command.Inputs.FirstOrDefault(
                item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(input.Key))
            {
                return input.Value;
            }
        }

        return string.Equals(key, "pullRequest", StringComparison.OrdinalIgnoreCase)
            ? command.PullRequest
            : null;
    }
}

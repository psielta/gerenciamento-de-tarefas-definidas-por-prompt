using FluentValidation;

namespace PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;

public sealed class GenerateMermaidDiagramValidator : AbstractValidator<GenerateMermaidDiagramCommand>
{
    public GenerateMermaidDiagramValidator()
    {
        RuleFor(c => c.Instruction).NotEmpty().MaximumLength(4_000);
        RuleFor(c => c.Model).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Temperature).InclusiveBetween(0.0, 2.0);
        RuleFor(c => c.CurrentCode)
            .MaximumLength(100_000)
            .When(c => c.CurrentCode is not null);
        RuleFor(c => c.DiagramKind)
            .Must(MermaidDiagramKinds.IsKnown)
            .When(c => !string.IsNullOrWhiteSpace(c.DiagramKind))
            .WithMessage("Tipo de diagrama Mermaid invalido.");
    }
}

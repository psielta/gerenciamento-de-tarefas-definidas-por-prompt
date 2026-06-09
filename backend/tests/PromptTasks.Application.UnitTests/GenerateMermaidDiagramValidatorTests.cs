using FluentAssertions;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.GenerateMermaidDiagram;

namespace PromptTasks.Application.UnitTests;

public sealed class GenerateMermaidDiagramValidatorTests
{
    private readonly GenerateMermaidDiagramValidator _validator = new();

    [Fact]
    public void Accepts_valid_command_with_known_kind()
    {
        var result = _validator.Validate(Command(diagramKind: "flowchart"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_empty_instruction()
    {
        var result = _validator.Validate(Command(instruction: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GenerateMermaidDiagramCommand.Instruction));
    }

    [Fact]
    public void Rejects_unknown_diagram_kind()
    {
        var result = _validator.Validate(Command(diagramKind: "gantt-chart-xyz"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GenerateMermaidDiagramCommand.DiagramKind));
    }

    [Fact]
    public void Rejects_current_code_over_limit()
    {
        var result = _validator.Validate(Command(currentCode: new string('a', 100_001)));

        result.IsValid.Should().BeFalse();
    }

    private static GenerateMermaidDiagramCommand Command(
        string instruction = "Fluxo de login",
        string? diagramKind = null,
        string? currentCode = null) =>
        new(
            instruction,
            diagramKind,
            "gemini-test",
            0.4,
            new GeminiThinking("none", null, null),
            null,
            null,
            currentCode);
}

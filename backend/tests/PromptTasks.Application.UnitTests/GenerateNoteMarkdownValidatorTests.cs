using FluentAssertions;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.GenerateNoteMarkdown;

namespace PromptTasks.Application.UnitTests;

public sealed class GenerateNoteMarkdownValidatorTests
{
    private readonly GenerateNoteMarkdownValidator _validator = new();

    [Fact]
    public void Accepts_valid_command_with_known_format()
    {
        var result = _validator.Validate(Command(format: "checklist"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_empty_instruction()
    {
        var result = _validator.Validate(Command(instruction: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GenerateNoteMarkdownCommand.Instruction));
    }

    [Fact]
    public void Rejects_instruction_over_limit()
    {
        var result = _validator.Validate(Command(instruction: new string('a', 4_001)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rejects_unknown_format()
    {
        var result = _validator.Validate(Command(format: "spreadsheet"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GenerateNoteMarkdownCommand.Format));
    }

    [Fact]
    public void Rejects_current_content_over_limit()
    {
        var result = _validator.Validate(Command(currentContent: new string('a', 100_001)));

        result.IsValid.Should().BeFalse();
    }

    private static GenerateNoteMarkdownCommand Command(
        string instruction = "Crie uma nota",
        string? format = null,
        string? currentContent = null) =>
        new(
            instruction,
            format,
            "gemini-test",
            0.4,
            new GeminiThinking("none", null, null),
            null,
            currentContent);
}

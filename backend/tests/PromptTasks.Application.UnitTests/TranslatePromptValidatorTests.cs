using FluentAssertions;
using PromptTasks.Application.Common.Models;
using PromptTasks.Application.Features.Ai.Commands.TranslatePrompt;

namespace PromptTasks.Application.UnitTests;

public sealed class TranslatePromptValidatorTests
{
    private readonly TranslatePromptValidator _validator = new();

    [Fact]
    public void Allows_valid_translation_request()
    {
        var result = _validator.Validate(CreateCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_empty_content()
    {
        var result = _validator.Validate(CreateCommand(content: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(TranslatePromptCommand.Content));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void Rejects_temperature_outside_range(double temperature)
    {
        var result = _validator.Validate(CreateCommand(temperature: temperature));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(TranslatePromptCommand.Temperature));
    }

    private static TranslatePromptCommand CreateCommand(
        string content = "Translate this",
        string model = "gemini-test",
        double temperature = 0.4) =>
        new(
            content,
            model,
            temperature,
            new GeminiThinking("none", 0, null));
}

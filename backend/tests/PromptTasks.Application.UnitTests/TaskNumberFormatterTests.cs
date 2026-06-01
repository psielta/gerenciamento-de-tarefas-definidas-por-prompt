using FluentAssertions;
using PromptTasks.Application.Features.Prompts;

namespace PromptTasks.Application.UnitTests;

public sealed class TaskNumberFormatterTests
{
    private static readonly DateOnly SampleDate = new(2026, 5, 28);

    [Theory]
    [InlineData("BP{N}{Date}", 1, "BP1280526")]
    [InlineData("BP{N:000}{Date}", 7, "BP007280526")]
    [InlineData("TASK-{N:00}-{Date:yyyyMMdd}", 12, "TASK-12-20260528")]
    [InlineData("TASK_{N}_{Date:dd-MM-yyyy}", 3, "TASK_3_28-05-2026")]
    public void Format_renders_supported_tokens(string pattern, int sequence, string expected)
    {
        var result = TaskNumberFormatter.Format(pattern, sequence, SampleDate);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("BP{Date}", "Pattern must include {N}.")]
    [InlineData("BP{N}", "Pattern must include {Date}.")]
    [InlineData("BP{X}{N}{Date}", "Unknown token {X}.")]
    [InlineData("BP/{N}{Date}", "Literal text may only contain letters, numbers, underscore and hyphen.")]
    [InlineData("BP {N}{Date}", "Literal text may only contain letters, numbers, underscore and hyphen.")]
    [InlineData("BP{N}{Date:yyyy/MM}", "{Date} only supports dd, MM, yy and yyyy tokens, optionally separated by hyphens.")]
    [InlineData("BP{N}{Date:MMMM}", "{Date} only supports dd, MM, yy and yyyy tokens, optionally separated by hyphens.")]
    [InlineData("BP{N}{Date:dddd}", "{Date} only supports dd, MM, yy and yyyy tokens, optionally separated by hyphens.")]
    [InlineData("BP{N:A}{Date}", "{N} only supports zero-fill formats like {N:000}.")]
    public void Validate_rejects_invalid_patterns(string pattern, string expectedError)
    {
        var result = TaskNumberFormatter.Validate(pattern);

        result.Should().Contain(expectedError);
    }

    [Fact]
    public void Validate_accepts_empty_pattern_as_disabled()
    {
        TaskNumberFormatter.Validate(null).Should().BeEmpty();
        TaskNumberFormatter.Validate("").Should().BeEmpty();
    }

    [Fact]
    public void Validate_rejects_generated_values_over_limit()
    {
        var literal = new string('A', 64);

        var result = TaskNumberFormatter.Validate($"{literal}{{N}}{{Date}}");

        result.Should().Contain("Generated task number must contain 1 to 64 URL-safe characters.");
    }
}

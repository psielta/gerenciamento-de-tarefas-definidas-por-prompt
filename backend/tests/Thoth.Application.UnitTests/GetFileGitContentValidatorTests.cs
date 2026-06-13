using FluentAssertions;
using Thoth.Application.Features.Git.Queries.GetFileGitContent;

namespace Thoth.Application.UnitTests;

public sealed class GetFileGitContentValidatorTests
{
    private readonly GetFileGitContentValidator _validator = new();

    [Theory]
    [InlineData("abcdef0")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0")]
    public void Accepts_valid_hashes(string hash)
    {
        var result = _validator.Validate(new GetFileGitContentQuery(Guid.CreateVersion7(), "src/app.ts", hash));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_null_hash()
    {
        var result = _validator.Validate(new GetFileGitContentQuery(Guid.CreateVersion7(), "src/app.ts", null!));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcdef")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef012")]
    [InlineData("not-a-hash")]
    [InlineData("HEAD")]
    [InlineData("-x")]
    [InlineData("abc~1")]
    [InlineData("abc:")]
    [InlineData("abcdef0\n")]
    public void Rejects_invalid_hashes(string hash)
    {
        var result = _validator.Validate(new GetFileGitContentQuery(Guid.CreateVersion7(), "src/app.ts", hash));

        result.IsValid.Should().BeFalse();
    }
}
using FluentAssertions;
using Thoth.Application.Features.Git;

namespace Thoth.Application.UnitTests;

public sealed class GitLogParserTests
{
    [Fact]
    public void Parse_maps_two_records()
    {
        var output =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\x1f" +
            "aaaaaaa\x1f" +
            "Author One\x1f" +
            "2026-01-01T00:00:00+00:00\x1f" +
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\x1f" +
            "Second commit\x0" +
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\x1f" +
            "bbbbbbb\x1f" +
            "Author Two\x1f" +
            "2025-12-31T00:00:00+00:00\x1f" +
            "\x1f" +
            "Root commit\x0";

        var result = GitLogParser.Parse(output);

        result.Should().HaveCount(2);
        result[0].Hash.Should().Be("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        result[0].ShortHash.Should().Be("aaaaaaa");
        result[0].Author.Should().Be("Author One");
        result[0].Date.Should().Be("2026-01-01T00:00:00+00:00");
        result[0].Message.Should().Be("Second commit");
        result[0].ParentHash.Should().Be("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        result[1].Hash.Should().Be("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        result[1].ParentHash.Should().BeEmpty();
        result[1].Message.Should().Be("Root commit");
    }

    [Fact]
    public void Parse_empty_output_returns_empty_list()
    {
        GitLogParser.Parse(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Parse_trailing_nul_returns_entries()
    {
        var output =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\x1f" +
            "aaaaaaa\x1f" +
            "Author\x1f" +
            "2026-01-01T00:00:00+00:00\x1f" +
            "\x1f" +
            "Only\x0";

        GitLogParser.Parse(output).Should().ContainSingle().Which.Message.Should().Be("Only");
    }

    [Fact]
    public void Parse_skips_records_with_fewer_than_six_fields()
    {
        var output = "hash\x1fshort\x1fauthor\x1fdate\x1fparent\x0";

        GitLogParser.Parse(output).Should().BeEmpty();
    }

    [Fact]
    public void Parse_allows_empty_subject()
    {
        var output =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\x1f" +
            "aaaaaaa\x1f" +
            "Author\x1f" +
            "2026-01-01T00:00:00+00:00\x1f" +
            "\x1f" +
            "\x0";

        GitLogParser.Parse(output).Should().ContainSingle().Which.Message.Should().BeEmpty();
    }

    [Fact]
    public void Parse_uses_first_parent_when_multiple_parents_present()
    {
        var output =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\x1f" +
            "aaaaaaa\x1f" +
            "Author\x1f" +
            "2026-01-01T00:00:00+00:00\x1f" +
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb cccccccccccccccccccccccccccccccccccccccc\x1f" +
            "Merge\x0";

        GitLogParser.Parse(output).Should().ContainSingle().Which.ParentHash
            .Should().Be("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
    }
}
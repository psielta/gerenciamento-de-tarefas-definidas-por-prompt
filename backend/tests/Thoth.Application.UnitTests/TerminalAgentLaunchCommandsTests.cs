using FluentAssertions;
using Thoth.Application.Features.Terminals;

namespace Thoth.Application.UnitTests;

public sealed class TerminalAgentLaunchCommandsTests
{
    [Theory]
    [InlineData(TerminalAgentLaunch.Claude, "claude --dangerously-skip-permissions --effort max\r")]
    [InlineData(TerminalAgentLaunch.Codex, "codex --yolo\r")]
    [InlineData(TerminalAgentLaunch.Grok, "grok --always-approve\r")]
    public void ResolveInitialInput_maps_known_agents(TerminalAgentLaunch agent, string expected)
    {
        var input = TerminalAgentLaunchCommands.ResolveInitialInput(agent);

        input.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(input!).Should().Be(expected);
    }

    [Theory]
    [InlineData("Claude", TerminalAgentLaunch.Claude)]
    [InlineData("codex", TerminalAgentLaunch.Codex)]
    [InlineData("GROK", TerminalAgentLaunch.Grok)]
    public void TryParse_accepts_supported_names(string value, TerminalAgentLaunch expected)
    {
        TerminalAgentLaunchCommands.TryParse(value, out var agent).Should().BeTrue();
        agent.Should().Be(expected);
    }
}
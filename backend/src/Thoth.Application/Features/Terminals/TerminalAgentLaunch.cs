namespace Thoth.Application.Features.Terminals;

public enum TerminalAgentLaunch
{
    Claude,
    Codex,
    Grok,
}

public static class TerminalAgentLaunchCommands
{
    public static bool TryParse(string? value, out TerminalAgentLaunch agent)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            agent = default;
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out agent);
    }

    public static byte[]? ResolveInitialInput(TerminalAgentLaunch? agent)
    {
        var command = agent switch
        {
            TerminalAgentLaunch.Claude => "claude --dangerously-skip-permissions --effort max\r",
            TerminalAgentLaunch.Codex => "codex --yolo\r",
            TerminalAgentLaunch.Grok => "grok --always-approve\r",
            _ => null,
        };

        return command is null ? null : System.Text.Encoding.UTF8.GetBytes(command);
    }
}
namespace Thoth.Infrastructure.Terminals;

public sealed class TerminalOptions
{
    public bool Enabled { get; set; }
    public bool AllowRemoteConnections { get; set; }
    public string[] AllowedShells { get; set; } = ["pwsh.exe", "powershell.exe"];
    public string DefaultShell { get; set; } = "pwsh.exe";
    public int MaxSessionsPerPrompt { get; set; } = 8;
    public int MaxTotalSessions { get; set; } = 32;
    public int OrphanTimeoutSeconds { get; set; } = 3600;
    public int OutputFlushMilliseconds { get; set; } = 25;
    public int MaxOutputChunkBytes { get; set; } = 8 * 1024;
    public int MaxInputBytes { get; set; } = 64 * 1024;
}
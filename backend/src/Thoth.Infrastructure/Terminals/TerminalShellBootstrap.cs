namespace Thoth.Infrastructure.Terminals;

public static class TerminalShellBootstrap
{
    public static bool IsPowerShell(string shell)
    {
        var fileName = Path.GetFileName(shell);
        return string.Equals(fileName, "pwsh.exe", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "powershell.exe", StringComparison.OrdinalIgnoreCase);
    }

    public static string[] BuildPowerShellStartupArgs(string cwd)
    {
        var escaped = cwd.Replace("'", "''", StringComparison.Ordinal);
        return new[] { "-NoLogo", "-NoExit", "-Command", $"Set-Location -LiteralPath '{escaped}'" };
    }
}
namespace Thoth.Infrastructure.Terminals;

public static class TerminalEnvironmentBootstrap
{
    public static Dictionary<string, string> BuildSpawnEnvironment(string? userProfileHint = null)
    {
        var environment = CopyCurrentProcessEnvironment();
        WindowsLoggedOnUserEnvironment.ApplyUserOverlay(environment, userProfileHint);

        foreach (var (key, value) in BuildColorOverrides())
        {
            environment[key] = value;
        }

        return environment;
    }

    public static Dictionary<string, string> BuildColorEnvironment(string? userProfileHint = null) =>
        BuildSpawnEnvironment(userProfileHint);

    public static Dictionary<string, string> BuildColorOverrides()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
            ["FORCE_COLOR"] = "1",
            ["CLICOLOR"] = "1",
            ["CLICOLOR_FORCE"] = "1",
            ["NO_COLOR"] = string.Empty,
        };
    }

    private static Dictionary<string, string> CopyCurrentProcessEnvironment()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                environment[key] = value;
            }
        }

        return environment;
    }
}
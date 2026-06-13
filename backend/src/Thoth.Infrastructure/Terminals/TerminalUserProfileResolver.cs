using Microsoft.Extensions.Configuration;

namespace Thoth.Infrastructure.Terminals;

public static class TerminalUserProfileResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        var explicitProfile = configuration["Terminals:UserProfile"];
        if (!string.IsNullOrWhiteSpace(explicitProfile))
        {
            return NormalizeProfilePath(explicitProfile);
        }

        if (TryGetUserProfileFromAgentPath(configuration["AgentUsage:Claude:CredentialsPath"], out var profile))
        {
            return profile;
        }

        if (TryGetUserProfileFromAgentPath(configuration["AgentUsage:Codex:SessionsDir"], out profile))
        {
            return profile;
        }

        if (TryGetUserProfileFromAgentPath(configuration["AgentUsage:Claude:ProjectsDir"], out profile))
        {
            return profile;
        }

        return null;
    }

    internal static bool TryGetUserProfileFromAgentPath(string? path, out string? profile)
    {
        profile = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var current = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(current))
        {
            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            if (string.Equals(parent.Name, "Users", StringComparison.OrdinalIgnoreCase))
            {
                profile = NormalizeProfilePath(current);
                return profile is not null;
            }

            current = parent.FullName;
        }

        return false;
    }

    private static string? NormalizeProfilePath(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(profile.Trim());
        return Directory.Exists(fullPath) ? fullPath : null;
    }
}
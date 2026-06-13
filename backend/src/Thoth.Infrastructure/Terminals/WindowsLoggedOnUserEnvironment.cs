using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Thoth.Infrastructure.Terminals;

internal static class WindowsLoggedOnUserEnvironment
{
    private static readonly string[] UserEnvironmentKeys =
    [
        "PATH",
        "PATHEXT",
        "USERPROFILE",
        "APPDATA",
        "LOCALAPPDATA",
        "HOME",
        "HOMEDRIVE",
        "HOMEPATH",
        "USERNAME",
        "USERDOMAIN",
        "USERDOMAIN_ROAMINGPROFILE",
        "OneDrive",
        "OneDriveConsumer",
        "OneDriveCommercial",
        "TEMP",
        "TMP",
    ];

    public static Dictionary<string, string>? TryLoad(string? hintedUserProfile)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var fromToken = WindowsUserEnvironmentBlock.TryCreate();
        if (fromToken is not null)
        {
            return fromToken;
        }

        var profilePath = NormalizeProfile(hintedUserProfile) ?? FindLoggedOnUserProfile();
        if (profilePath is null)
        {
            return null;
        }

        var sid = FindSidForProfile(profilePath);
        if (sid is not null)
        {
            var fromRegistry = ReadEnvironmentFromUserSid(sid);
            if (fromRegistry is not null)
            {
                return fromRegistry;
            }
        }

        return SynthesizeUserEnvironment(profilePath);
    }

    public static void ApplyUserOverlay(Dictionary<string, string> environment, string? userProfileHint)
    {
        var overlay = TryLoad(userProfileHint);
        if (overlay is null)
        {
            return;
        }

        foreach (var key in UserEnvironmentKeys)
        {
            if (string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (overlay.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                environment[key] = value;
            }
        }

        if (overlay.TryGetValue("PATH", out var overlayPath))
        {
            environment["PATH"] = MergePath(overlayPath, environment.GetValueOrDefault("PATH"));
        }
    }

    private static Dictionary<string, string>? ReadEnvironmentFromUserSid(string sid)
    {
        using var environmentKey = Registry.Users.OpenSubKey($@"{sid}\Environment");
        if (environmentKey is null)
        {
            return null;
        }

        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in environmentKey.GetValueNames())
        {
            if (environmentKey.GetValue(name) is string value)
            {
                environment[name] = value;
            }
        }

        return environment.Count == 0 ? null : environment;
    }

    private static Dictionary<string, string> SynthesizeUserEnvironment(string userProfile)
    {
        var userName = Path.GetFileName(userProfile.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var appData = Path.Combine(userProfile, "AppData", "Roaming");
        var localAppData = Path.Combine(userProfile, "AppData", "Local");

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["USERPROFILE"] = userProfile,
            ["HOME"] = userProfile,
            ["APPDATA"] = appData,
            ["LOCALAPPDATA"] = localAppData,
            ["HOMEDRIVE"] = Path.GetPathRoot(userProfile)?.TrimEnd('\\') ?? "C:",
            ["HOMEPATH"] = userProfile.Length > 3 ? userProfile[3..] : userProfile,
            ["USERNAME"] = userName,
            ["USERDOMAIN"] = Environment.MachineName,
            ["USERDOMAIN_ROAMINGPROFILE"] = Environment.MachineName,
            ["TEMP"] = Path.Combine(localAppData, "Temp"),
            ["TMP"] = Path.Combine(localAppData, "Temp"),
            ["PATH"] = string.Join(';', BuildDefaultPathSegments(userProfile)),
            ["PATHEXT"] = Environment.GetEnvironmentVariable("PATHEXT") ??
                           ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.CPL",
        };
    }

    private static IEnumerable<string> BuildDefaultPathSegments(string userProfile)
    {
        yield return Path.Combine(userProfile, "AppData", "Local", "Microsoft", "WindowsApps");
        yield return Path.Combine(userProfile, "AppData", "Roaming", "npm");
        yield return Path.Combine(userProfile, ".local", "bin");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System));
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
    }

    private static string? FindLoggedOnUserProfile()
    {
        foreach (var sid in Registry.Users.GetSubKeyNames())
        {
            if (!sid.StartsWith("S-1-5-21-", StringComparison.Ordinal))
            {
                continue;
            }

            if (sid.Contains("_Classes", StringComparison.Ordinal))
            {
                continue;
            }

            using var environmentKey = Registry.Users.OpenSubKey($@"{sid}\Environment");
            var userProfile = environmentKey?.GetValue("USERPROFILE") as string;
            if (!string.IsNullOrWhiteSpace(userProfile) && Directory.Exists(userProfile))
            {
                return userProfile;
            }
        }

        return null;
    }

    private static string? FindSidForProfile(string profilePath)
    {
        using var profileList = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
        if (profileList is null)
        {
            return null;
        }

        foreach (var sid in profileList.GetSubKeyNames())
        {
            using var profileKey = profileList.OpenSubKey(sid);
            var imagePath = profileKey?.GetValue("ProfileImagePath") as string;
            if (string.Equals(imagePath, profilePath, StringComparison.OrdinalIgnoreCase))
            {
                return sid;
            }
        }

        return null;
    }

    private static string? NormalizeProfile(string? profilePath)
    {
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(profilePath);
        return Directory.Exists(fullPath) ? fullPath : null;
    }

    private static string MergePath(string? basePath, string? overlayPath)
    {
        var segments = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var segment in SplitPath(overlayPath).Concat(SplitPath(basePath)))
        {
            if (seen.Add(segment))
            {
                segments.Add(segment);
            }
        }

        return string.Join(';', segments);
    }

    private static IEnumerable<string> SplitPath(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
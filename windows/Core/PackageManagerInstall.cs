namespace QuadClicker.Core;

/// <summary>
/// Detects an install owned by Scoop, Chocolatey or winget. Those tools update the app
/// themselves, so the in-app self-updater must stay off or the two fight (QCLK-9).
/// </summary>
public static class PackageManagerInstall
{
    // Lower-case fragments of the exe path each manager installs under.
    private static readonly string[] Markers =
    {
        @"\scoop\apps\",
        @"\scoop\persist\",
        @"\chocolatey\lib\",
        @"\winget\packages\",
    };

    public static bool IsManaged(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        string p = exePath.Replace('/', '\\').ToLowerInvariant();
        foreach (var m in Markers)
            if (p.Contains(m, StringComparison.Ordinal)) return true;
        return false;
    }

    public static bool IsCurrentProcessManaged() => IsManaged(Environment.ProcessPath);
}

namespace CrystalBrowser.App;

/// <summary>
/// Defines which historical Crystal versions are no longer supported.
///
/// Versions <b>1.0 through 1.3</b> are end-of-life: they receive no further fixes or
/// updates and are considered unsupported. The first supported version is 1.4.
/// </summary>
public static class SupportPolicy
{
    /// <summary>The oldest version that is still supported. Anything below this is unsupported.</summary>
    public const string MinimumSupportedVersion = "1.4";

    /// <summary>
    /// Returns <c>true</c> if <paramref name="version"/> is one of the unsupported
    /// 1.0–1.3 builds (i.e. older than <see cref="MinimumSupportedVersion"/>).
    /// </summary>
    public static bool IsUnsupported(string? version)
    {
        if (System.Version.TryParse(Normalize(version), out var v) &&
            System.Version.TryParse(MinimumSupportedVersion, out var min))
            return v < min;

        // If we can't parse it, don't claim it's unsupported.
        return false;
    }

    /// <summary>Convenience inverse of <see cref="IsUnsupported"/>.</summary>
    public static bool IsSupported(string? version) => !IsUnsupported(version);

    // Trim a leading "v" and ensure there's at least a major.minor so "1" parses as "1.0".
    private static string Normalize(string? version)
    {
        version = (version ?? string.Empty).Trim().TrimStart('v', 'V');
        return version.Contains('.') ? version : version + ".0";
    }
}

using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace CrystalBrowser.App;

/// <summary>
/// Helpers behind the Chrome-style "make Crystal Browser your default browser" nag.
/// Windows 10/11 no longer let an app silently grab the default-browser association, so
/// (like Chrome) we detect whether we're already the default and, if not, deep-link the
/// user to the Windows "Default apps" settings page. A persisted flag lets the user
/// dismiss the nag for good.
/// </summary>
public static class DefaultBrowser
{
    private static readonly string FlagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrystalBrowser", "nag-dismissed");

    /// <summary>
    /// True if Crystal Browser currently handles the http(s) association for this user.
    /// Reads the Windows UserChoice ProgId; matches our own ProgId or exe path.
    /// </summary>
    public static bool IsDefault()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
            var progId = key?.GetValue("ProgId") as string;
            return !string.IsNullOrEmpty(progId) &&
                   progId.Contains("Crystal", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Has the user permanently dismissed the nag?</summary>
    public static bool NagDismissed() => File.Exists(FlagPath);

    /// <summary>Remember that the user clicked "No thanks" so we stop nagging.</summary>
    public static void DismissNag()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FlagPath)!);
            File.WriteAllText(FlagPath, DateTime.UtcNow.ToString("o"));
        }
        catch { /* best effort */ }
    }

    /// <summary>Show the nag? Only when we're not default and the user hasn't dismissed it.</summary>
    public static bool ShouldNag() => !IsDefault() && !NagDismissed();

    /// <summary>
    /// Open the Windows "Default apps" settings so the user can pick Crystal Browser,
    /// exactly like Chrome's banner does.
    /// </summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:defaultapps",
                UseShellExecute = true,
            });
        }
        catch { /* settings app unavailable — nothing we can do */ }
    }
}

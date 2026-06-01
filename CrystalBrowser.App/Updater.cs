using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>Details of an available update found on GitHub Releases.</summary>
public record UpdateInfo(string Version, string DownloadUrl, string PageUrl);

/// <summary>Outcome of an update check.</summary>
public enum UpdateStatus
{
    /// <summary>The running version is the latest — no need to keep polling.</summary>
    UpToDate,
    /// <summary>A newer release with an installer is available.</summary>
    Available,
    /// <summary>The check itself didn't complete (offline, rate-limited, asset not ready) —
    /// no definitive answer, so the caller should keep polling.</summary>
    Failed,
}

/// <summary>Result of an update check: a status plus, when available, the update details.</summary>
public record UpdateResult(UpdateStatus Status, UpdateInfo? Info);

/// <summary>
/// In-app updater. Checks the configured GitHub repo's latest Release, compares its tag to
/// the running version, and (if newer) downloads the installer and launches it.
/// </summary>
public static class Updater
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub's API requires a User-Agent.
        c.DefaultRequestHeaders.UserAgent.ParseAdd("CrystalBrowser-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>
    /// Check GitHub for a newer release. Distinguishes "up to date" from "couldn't check" so
    /// callers can stop polling once they have a definitive answer but keep retrying on failure.
    /// </summary>
    public static async Task<UpdateResult> CheckAsync()
    {
        // Checks disabled — treat as a definitive "nothing to do" so polling stops.
        if (string.IsNullOrWhiteSpace(Config.UpdateRepo))
            return new UpdateResult(UpdateStatus.UpToDate, null);

        try
        {
            var url = $"https://api.github.com/repos/{Config.UpdateRepo}/releases/latest";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var latest = ParseVersion(tag);
            if (latest == null)
                return new UpdateResult(UpdateStatus.Failed, null); // unparseable tag — retry later

            // We're already on (or ahead of) the latest release: definitive, stop polling.
            if (latest <= ParseVersion(Config.Version))
                return new UpdateResult(UpdateStatus.UpToDate, null);

            // Find the installer asset (an .exe).
            string? dl = null;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        dl = a.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

            var page = root.GetProperty("html_url").GetString() ?? "";
            // Newer release exists but its installer isn't uploaded yet — keep polling.
            if (dl == null)
                return new UpdateResult(UpdateStatus.Failed, null);

            return new UpdateResult(UpdateStatus.Available,
                new UpdateInfo(tag.TrimStart('v', 'V'), dl, page));
        }
        catch
        {
            // Offline, rate-limited, etc. — no answer, so the caller should keep pinging.
            return new UpdateResult(UpdateStatus.Failed, null);
        }
    }

    /// <summary>Download the installer to a temp file and run it. Returns false on failure.</summary>
    public static async Task<bool> DownloadAndRunAsync(UpdateInfo info)
    {
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "CrystalBrowserSetup-update.exe");
            var bytes = await Http.GetByteArrayAsync(info.DownloadUrl);
            await File.WriteAllBytesAsync(tmp, bytes);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tmp,
                UseShellExecute = true, // allow the installer's UAC/SmartScreen prompt
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Parse "v1.2.3" / "1.2" into a comparable Version; null if unparseable.
    private static Version? ParseVersion(string s)
    {
        s = s.Trim().TrimStart('v', 'V');
        return Version.TryParse(s.Contains('.') ? s : s + ".0", out var v) ? v : null;
    }
}

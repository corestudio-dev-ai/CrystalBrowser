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
            var local = ParseVersion(Config.Version);
            if (latest == null)
                return new UpdateResult(UpdateStatus.Failed, null); // unparseable tag — retry later

            // We're ahead of the latest release: definitive, stop polling.
            if (latest < local)
                return new UpdateResult(UpdateStatus.UpToDate, null);

            root.TryGetProperty("assets", out var assets);

            // Same version: this can still be an *emergency patch* (the version is intentionally
            // kept the same and only the assets are replaced). Detect it via the published patch
            // level in the release's patch.json asset — if it's higher than ours, update.
            if (latest == local)
            {
                int remotePatch = await ReadPatchLevelAsync(assets);
                if (remotePatch <= Config.PatchLevel)
                    return new UpdateResult(UpdateStatus.UpToDate, null);
                // else: an emergency patch is available — fall through to grab the installer.
            }

            // Find the installer asset (an .exe).
            string? dl = null;
            if (assets.ValueKind == JsonValueKind.Array)
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

    /// <summary>
    /// Read the emergency-patch level published with a release, from its <c>patch.json</c> asset
    /// (e.g. <c>{ "version": "1.8.1", "patch": 2 }</c>). Returns 0 when there's no such asset or it
    /// can't be read, so a release without the file is simply treated as patch level 0.
    /// </summary>
    private static async Task<int> ReadPatchLevelAsync(JsonElement assets)
    {
        try
        {
            if (assets.ValueKind != JsonValueKind.Array) return 0;
            string? patchUrl = null;
            foreach (var a in assets.EnumerateArray())
            {
                if (string.Equals(a.GetProperty("name").GetString(), "patch.json", StringComparison.OrdinalIgnoreCase))
                {
                    patchUrl = a.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
            if (patchUrl == null) return 0;
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(patchUrl));
            return doc.RootElement.TryGetProperty("patch", out var p) && p.TryGetInt32(out var n) ? n : 0;
        }
        catch { return 0; }
    }

    // Parse "v1.2.3" / "1.2" into a comparable Version; null if unparseable.
    private static Version? ParseVersion(string s)
    {
        s = s.Trim().TrimStart('v', 'V');
        return Version.TryParse(s.Contains('.') ? s : s + ".0", out var v) ? v : null;
    }
}

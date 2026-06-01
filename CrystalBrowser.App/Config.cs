namespace CrystalBrowser.App;

/// <summary>Shared app-wide configuration constants.</summary>
public static class Config
{
    /// <summary>Search provider used by the address bar and home page (query appended).</summary>
    public const string SearchUrl = "https://www.google.com/search?q=";

    /// <summary>This build's version. Bump it each release (and tag the GitHub release to match).</summary>
    public const string Version = "1.3";

    /// <summary>
    /// GitHub repo (owner/name) whose Releases the in-app updater checks. Set this to your
    /// repo, e.g. "hyperr10/CrystalBrowser". Leave blank to disable update checks.
    /// </summary>
    public const string UpdateRepo = "corestudio-dev-ai/CrystalBrowser";
}

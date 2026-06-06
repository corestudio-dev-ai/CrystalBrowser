namespace CrystalBrowser.App;

/// <summary>Shared app-wide configuration constants.</summary>
public static class Config
{
    /// <summary>Default search provider (Google) used by the address bar and home page.</summary>
    public const string SearchUrl = "https://www.google.com/search?q=";

    /// <summary>The query-prefix URL for a search engine key ("google" or "duckduckgo").</summary>
    public static string SearchQueryUrl(string engine) => engine == "duckduckgo"
        ? "https://duckduckgo.com/?q="
        : "https://www.google.com/search?q=";

    /// <summary>The &lt;form&gt; action URL for a search engine key (the offline pages GET to it).</summary>
    public static string SearchFormAction(string engine) => engine == "duckduckgo"
        ? "https://duckduckgo.com/"
        : "https://www.google.com/search";

    /// <summary>Friendly display name for a search engine key.</summary>
    public static string SearchName(string engine) => engine == "duckduckgo" ? "DuckDuckGo" : "Google";

    /// <summary>This build's version. Bump it each release (and tag the GitHub release to match).</summary>
    public const string Version = "1.8.1";

    /// <summary>
    /// Emergency-patch level *within* the current <see cref="Version"/>. Starts at 0 for a fresh
    /// version and increments by 1 for every emergency patch that re-ships the SAME version (see the
    /// "Emergency patches" section in CLAUDE.md). Reset back to 0 whenever <see cref="Version"/> is
    /// bumped. The updater treats a release whose published patch level is higher than this — at the
    /// same version — as an available update, so emergency patches reach existing installs.
    /// </summary>
    public const int PatchLevel = 2;

    /// <summary>
    /// GitHub repo (owner/name) whose Releases the in-app updater checks. Set this to your
    /// repo, e.g. "hyperr10/CrystalBrowser". Leave blank to disable update checks.
    /// </summary>
    public const string UpdateRepo = "corestudio-dev-ai/CrystalBrowser";
}

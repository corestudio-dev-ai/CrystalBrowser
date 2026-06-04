using System.Text;

namespace CrystalBrowser.App;

/// <summary>One released version and the user-facing changes it shipped.</summary>
public record ChangelogEntry(string Version, string[] Changes);

/// <summary>
/// The in-app changelog. This is the source of truth shown to users — after an update Crystal
/// opens the "What's new" page, and the full history is always available from Settings.
///
/// IMPORTANT: add a new entry at the TOP of <see cref="Entries"/> for every release, describing
/// (in plain language) what actually changed. Keep it in sync with <c>Config.Version</c>.
/// </summary>
public static class Changelog
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries = new[]
    {
        new ChangelogEntry("1.7", new[]
        {
            "A full visual redesign: the neon \"gamer\" look is gone, replaced by a calm indigo accent and a clean, flat interface.",
            "Window transparency — Crystal now uses the Windows 11 Mica backdrop for a modern, translucent feel.",
            "Choose your own window frame colour in Settings.",
            "Redesigned home page and a refreshed welcome flow that announces the version with a smooth animation.",
            "Reset Crystal: restore all settings to defaults and restart, right from Settings.",
            "Internal pages: type crystal://whatsnew to revisit this page any time (also crystal://settings and crystal://home).",
        }),
        new ChangelogEntry("1.6.4", new[]
        {
            "Added an in-app changelog. After each update Crystal shows what's new, and you can review the full history any time from Settings.",
        }),
        new ChangelogEntry("1.6.3.1", new[]
        {
            "Labelled the AI sidebar's toolbar button \"AI\" so it's clear what it opens.",
        }),
        new ChangelogEntry("1.6.3", new[]
        {
            "Reordered the AI sidebar to Gemini, ChatGPT, then Claude — Gemini is now the default.",
            "Claude now gently asks you to sign in at claude.ai first instead of showing a bare login page; it's available in the sidebar once you're signed in.",
        }),
        new ChangelogEntry("1.6.2", new[]
        {
            "Added an AI companion sidebar with your choice of Gemini, ChatGPT, or Claude — like Edge's Copilot.",
        }),
        new ChangelogEntry("1.6.1", new[]
        {
            "Cleaned up the first-run onboarding for a more polished look.",
        }),
        new ChangelogEntry("1.6", new[]
        {
            "Preset color themes: Crystal, Light, Midnight, Forest, and Rose.",
            "Choose Google or DuckDuckGo as your search engine.",
            "A first-run onboarding flow that walks you through Crystal's features.",
            "Import your bookmarks from an installed Chrome, Edge, or Brave.",
        }),
    };

    /// <summary>The newest released version (top of the list).</summary>
    public static string Latest => Entries.Count > 0 ? Entries[0].Version : Config.Version;

    /// <summary>Shared CSS for the changelog list, used by both the page and the Settings card.</summary>
    public const string Css = """
  .cl { padding:14px 0; border-bottom:1px solid rgba(255,255,255,.06); }
  .cl:last-child { border-bottom:0; }
  .clv { font-size:15px; font-weight:700; color:#fff; display:flex; align-items:center; gap:9px; }
  .clv .new { font-size:11px; font-weight:600; color:#fff; background:var(--accent);
    padding:2px 9px; border-radius:9px; }
  .cl ul { margin:9px 0 0 18px; padding:0; }
  .cl li { font-size:14px; color:#cbc9ec; line-height:1.6; margin:3px 0; }
""";

    /// <summary>Render the changelog entries as HTML; the entry matching <paramref name="highlight"/>
    /// (if any) gets a "New" badge.</summary>
    public static string EntriesHtml(string? highlight)
    {
        var sb = new StringBuilder();
        foreach (var e in Entries)
        {
            bool hi = highlight != null && e.Version == highlight;
            sb.Append("<div class=\"cl\">");
            sb.Append($"<div class=\"clv\">Version {Esc(e.Version)}{(hi ? "<span class=\"new\">New</span>" : "")}</div>");
            sb.Append("<ul>");
            foreach (var c in e.Changes) sb.Append($"<li>{Esc(c)}</li>");
            sb.Append("</ul></div>");
        }
        return sb.ToString();
    }

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

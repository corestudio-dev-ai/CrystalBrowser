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
        new ChangelogEntry("2.0", new[]
        {
            "Crystal Browser 2.0 ULTRA — a complete redesign with a brand-new look.",
            "Bright by default: the new \"Ultra\" light theme is now the default — clean white frosted glass with a vivid blue accent. Light and Dark are still one click away in Settings.",
            "A fully custom, borderless window: no chunky Windows frame, smooth rounded corners, and redesigned minimize / maximize / close buttons.",
            "Frosted glass everywhere: the toolbar, tab rail and panels are translucent so your desktop gently shows through.",
            "A redesigned Settings page that matches the new Ultra look.",
            "Performance: retired the old animated background and trimmed needless work while you browse, so Crystal feels lighter and quicker.",
        }),
        new ChangelogEntry("1.8.2", new[]
        {
            "The update banner is now Oceanic blue to match the rest of the browser (no more leftover purple).",
            "Update notices now show patch levels clearly — e.g. \"1.8.1 [patch 2]\" — so you can tell a small patched update apart from a full new version.",
            "Fixed tab hover: hovering a tab no longer flips it to a dark style, so titles stay readable instead of turning white-on-white.",
        }),
        new ChangelogEntry("1.8.1", new[]
        {
            "New default look: a bright \"Oceanic\" theme — blue, white and transparent, like liquid glass over your desktop.",
            "Themes simplified to three clean choices: Oceanic, Light and Dark. The old neon and heavily-coloured themes have been retired.",
            "Tabs now always live in the vertical rail for a cleaner, more vertical layout — the top tab strip is gone.",
            "A redesigned bookmarks bar with rounded, accent-dotted chips.",
            "New screenshot tool next to the Edit pen: click it to instantly save a picture of the page to Documents\\Crystal Browser Screens.",
            "The Oceanic theme now gently flows: a soft, animated blue \"liquid glass\" water effect shimmers behind the interface.",
        }),
        new ChangelogEntry("1.8", new[]
        {
            "A big redesign inspired by Zen Browser: a calmer, cleaner look with vertical tabs now the default layout.",
            "New default accent colour — a warm Crystal orange — used across the interface.",
            "Crystal autofill: Crystal can now save your website logins and fill them in automatically. Passwords are encrypted on your device with your Windows account, and you can review or remove saved logins from Settings → Saved passwords.",
            "Tab groups: right-click any tab to put it in a coloured, collapsible group in the vertical tab rail — great for keeping related tabs together.",
            "The logo now carries a red accent, and the window close button turns black on hover to match the new dark look.",
        }),
        new ChangelogEntry("1.7.4", new[]
        {
            "Older versions (1.0–1.3) are now marked unsupported. If you're on one of those builds, Crystal will gently remind you to update each time it opens — but it never blocks you, and those versions stay fully usable and downloadable.",
        }),
        new ChangelogEntry("1.7.3.2", new[]
        {
            "Fixed the doubled window buttons for good. Windows was drawing its own minimize/maximize/close buttons on top of Crystal's; the title bar no longer extends the system frame, so only Crystal's own buttons show. The Mica transparency is unchanged.",
        }),
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

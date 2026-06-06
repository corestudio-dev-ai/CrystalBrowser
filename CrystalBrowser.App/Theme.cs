namespace CrystalBrowser.App;

/// <summary>
/// A named UI theme. Drives both the WPF chrome (the surface brushes in
/// <see cref="MainWindow.ApplyTheme"/>) and the offline NavigateToString pages (via
/// <see cref="Theme.PageCss"/>). Each theme bundles a background, an accent and a chrome palette.
/// </summary>
public sealed record ThemeDef(
    string Key, string Name, bool Light, string Accent, string Accent2, string PageBg,
    string WindowBg, string ChromeBg, string ChromeBg2, string SurfaceBg,
    string TextPrimary, string TextMuted, string TabTextActive, string TabTextInactive, string TabActiveBg);

/// <summary>Theme registry + shared theming helpers for the offline (NavigateToString) pages.</summary>
public static class Theme
{
    /// <summary>All selectable themes, in display order. "oceanic" (liquid-glass blue) is the default.</summary>
    public static readonly IReadOnlyList<ThemeDef> All = new[]
    {
        // Oceanic — a light "liquid glass" theme: blue/white with translucent chrome that lets
        // the Win11 Mica backdrop shimmer through (the 8-digit #AARRGGBB colours carry alpha).
        new ThemeDef("oceanic", "Oceanic", true, "#1E88E5", "#82C4FF",
            "radial-gradient(1200px 700px at 50% -10%, #d6ecfb 0%, #e9f4fc 55%, #f4f9fd 100%)",
            "#eef5fb", "#CCFFFFFF", "#B3E6F2FC", "#E6FFFFFF",
            "#0f2a40", "#5b7186", "#0f2a40", "#4a6275", "#CCFFFFFF"),
        new ThemeDef("light", "Light", true, "#1E88E5", "#5AA9F0",
            "#f3f6fa",
            "#f3f6fa", "#e7edf4", "#eef2f8", "#ffffff",
            "#16202b", "#5b6b7b", "#16202b", "#5b6b7b", "#dde6f0"),
        new ThemeDef("dark", "Dark", false, "#4F9BFF", "#8FC4FF",
            "radial-gradient(1200px 700px at 50% -10%, #14202e 0%, #0f1722 55%, #0b1016 100%)",
            "#0b1016", "#161b22", "#11151b", "#1a2029",
            "#e6edf3", "#8b949e", "#ffffff", "#b3bcc7", "#1f2630"),
    };

    /// <summary>Look up a theme by key, falling back to the default Oceanic theme.</summary>
    public static ThemeDef Get(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    /// <summary>True if the given theme key is a light theme (pages render normally, no force-dark).</summary>
    public static bool IsLight(string? key) => Get(key).Light;

    /// <summary>
    /// A &lt;style&gt; block that re-skins the shared offline page classes for the given theme:
    /// the body background and accent variables, plus light-mode overrides for the light theme.
    /// </summary>
    public static string PageCss(string? themeKey) => $"<style>\n{PageCssInner(themeKey)}\n</style>";

    /// <summary>The CSS rules of <see cref="PageCss"/> without the &lt;style&gt; wrapper, so the
    /// onboarding page can hot-swap them live (via its <c>#themecss</c> element).</summary>
    public static string PageCssInner(string? themeKey)
    {
        var t = Get(themeKey);
        // Use the user's chosen accent (which the theme sets, but the swatches can override).
        var accent = SettingsStore.Current.Accent;
        var light = t.Light ? """
  body { background:#f3f2f8 !important; color:#1c1a2e !important; }
  .card,.gauge,.badge,.tile,.steps,.seg,.sw { background:rgba(0,0,0,.04) !important; }
  .card,.gauge,.badge,.tile,.steps { border-color:rgba(0,0,0,.08) !important; }
  .k,.sub,.note,.tag,.greeting,.ust,.lead,.card h2,.badge .d { color:#5b5878 !important; }
  .v,.clock,.tile,.gauge .val,.badge .t { color:#1c1a2e !important; }
  input { background:#fff !important; color:#1c1a2e !important; border-color:rgba(0,0,0,.15) !important; }
  .row,.profrow { border-color:rgba(0,0,0,.08) !important; }
  .seg button { color:#5b5878; }
  a { color:#1E88E5 !important; }
""" : "";
        // --accent is set without !important so a page's live inline override (the accent
        // swatches on the Settings page) still wins; the body background is themed.
        return $$"""
  :root { --accent:{{accent}}; --accent2:{{t.Accent2}}; }
  body { background:{{t.PageBg}} !important; }
{{light}}
""";
    }
}

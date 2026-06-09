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
    /// <summary>
    /// All selectable themes, in display order. "ultra" — a bright, frosted-glass light theme —
    /// is the default (Crystal Browser 2.0 ULTRA). The chrome colours carry alpha (#AARRGGBB) so
    /// the Win11 Mica backdrop frosts through the translucent panels.
    /// </summary>
    public static readonly IReadOnlyList<ThemeDef> All = new[]
    {
        // Ultra — the 2.0 flagship: clean white frosted glass with a vivid blue accent.
        new ThemeDef("ultra", "Ultra", true, "#2F6BFF", "#8FB6FF",
            "radial-gradient(1200px 760px at 50% -12%, #eaf1ff 0%, #f3f7fe 52%, #fbfdff 100%)",
            "#f5f8fe", "#CCFFFFFF", "#A6FFFFFF", "#E6FFFFFF",
            "#0f1b2d", "#5a6b80", "#0f1b2d", "#5a6b80", "#FFFFFFFF"),
        // Light — a flat, fully opaque light theme for low-power / no-transparency setups.
        new ThemeDef("light", "Light", true, "#2F6BFF", "#5AA0F0",
            "#f3f6fa",
            "#f3f6fa", "#e9eef5", "#f1f5fa", "#ffffff",
            "#16202b", "#5b6b7b", "#16202b", "#5b6b7b", "#ffffff"),
        // Dark — frosted dark glass over the Mica backdrop.
        new ThemeDef("dark", "Dark", false, "#5B8CFF", "#8FC4FF",
            "radial-gradient(1200px 760px at 50% -12%, #141d2b 0%, #0f1722 55%, #0b1016 100%)",
            "#0c1118", "#CC141A24", "#B30E131B", "#E61A2030",
            "#e6edf3", "#8b949e", "#ffffff", "#b3bcc7", "#1f2630"),
    };

    /// <summary>Look up a theme by key, falling back to the default Ultra theme.</summary>
    public static ThemeDef Get(string? key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    /// <summary>True if the given theme key is a light theme (pages render normally, no force-dark).</summary>
    public static bool IsLight(string? key) => Get(key).Light;

    /// <summary>
    /// A &lt;style&gt; block that re-skins the shared offline page classes for the given theme:
    /// the body background and accent variables, plus light-mode overrides for light themes.
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
  body { background:#f4f7fd !important; color:#0f1b2d !important; }
  .card,.gauge,.badge,.tile,.steps,.seg,.sw { background:rgba(255,255,255,.7) !important; }
  .card,.gauge,.badge,.tile,.steps { border-color:rgba(15,27,45,.08) !important;
    box-shadow:0 8px 30px rgba(31,60,110,.06) !important; }
  .k,.sub,.note,.tag,.greeting,.ust,.lead,.card h2,.badge .d { color:#5a6b80 !important; }
  .v,.clock,.tile,.gauge .val,.badge .t { color:#0f1b2d !important; }
  input { background:rgba(255,255,255,.85) !important; color:#0f1b2d !important; border-color:rgba(15,27,45,.14) !important; }
  .row,.profrow { border-color:rgba(15,27,45,.08) !important; }
  .seg button { color:#5a6b80; }
  a { color:#2F6BFF !important; }
  .clv { color:#0f1b2d !important; }
  .cl, .cl li { color:#41526a !important; border-color:rgba(15,27,45,.08) !important; }
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

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
    /// All selectable themes, in display order. "aero" — true see-through glass over the Win11
    /// acrylic backdrop — is the default (Crystal Browser 2.1 AERO). The chrome colours carry
    /// alpha (#AARRGGBB) so the desktop frosts through the translucent panels. Crystal is
    /// light-only as of 2.1 — dark mode has been retired.
    /// </summary>
    public static readonly IReadOnlyList<ThemeDef> All = new[]
    {
        // Aero — the 2.1 flagship: real see-through acrylic glass with a vivid blue accent.
        new ThemeDef("aero", "Aero", true, "#2F6BFF", "#8FB6FF",
            "radial-gradient(1100px 700px at 50% -10%, #dfeaff 0%, #eef4ff 45%, #f8fbff 100%)",
            "#C9FFFFFF", "#A6FFFFFF", "#8CFFFFFF", "#D9FFFFFF",
            "#0f1b2d", "#5a6b80", "#0f1b2d", "#5a6b80", "#F2FFFFFF"),
        // Light — a flat, fully opaque light theme for low-power / no-transparency setups.
        new ThemeDef("light", "Light", true, "#2F6BFF", "#5AA0F0",
            "#f3f6fa",
            "#f3f6fa", "#e9eef5", "#f1f5fa", "#ffffff",
            "#16202b", "#5b6b7b", "#16202b", "#5b6b7b", "#ffffff"),
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
        // Light re-skin for the shared offline page classes. (The body background itself comes
        // from the theme's PageBg above, so gradient themes like Aero keep their gradient.)
        var light = t.Light ? """
  body { color:#0f1b2d !important; }
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

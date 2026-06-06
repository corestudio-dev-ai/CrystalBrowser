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
    /// <summary>All selectable themes, in display order. "dark" (Crystal) is the default.</summary>
    public static readonly IReadOnlyList<ThemeDef> All = new[]
    {
        new ThemeDef("dark", "Crystal", false, "#FF7A1A", "#FFA85C",
            "radial-gradient(1200px 700px at 50% -10%, #2a1c12 0%, #1a130d 55%, #0e0b08 100%)",
            "#0e0b08", "#1f1813", "#17110c", "#130e0a",
            "#fff3ea", "#a68a78", "#ffffff", "#dac6b9", "#1f1813"),
        new ThemeDef("light", "Light", true, "#5A47E0", "#7C6CFF",
            "#f3f2f8",
            "#f3f2f8", "#e7e5f1", "#eceaf5", "#ffffff",
            "#1a1830", "#6b6890", "#1a1830", "#6b6890", "#dcd9ec"),
        new ThemeDef("midnight", "Midnight", false, "#00E5FF", "#6CF0FF",
            "radial-gradient(1200px 700px at 50% -10%, #07243a 0%, #061522 55%, #02080f 100%)",
            "#02080f", "#0a1b2b", "#07131f", "#050d16",
            "#e6f6ff", "#6f8ba0", "#ffffff", "#9fb6c8", "#0a1b2b"),
        new ThemeDef("forest", "Forest", false, "#69F0AE", "#38C172",
            "radial-gradient(1200px 700px at 50% -10%, #123524 0%, #0c2018 55%, #07140e 100%)",
            "#07140e", "#10271c", "#0b1d15", "#0a1810",
            "#e8fff0", "#7ea08b", "#ffffff", "#a6c8b5", "#10271c"),
        new ThemeDef("rose", "Rose", false, "#FF3D7E", "#FF89B0",
            "radial-gradient(1200px 700px at 50% -10%, #3a1024 0%, #240b18 55%, #14070d 100%)",
            "#14070d", "#2a1019", "#1d0b12", "#180a10",
            "#ffe9f1", "#a67e8d", "#ffffff", "#c8a6b5", "#2a1019"),
    };

    /// <summary>Look up a theme by key, falling back to the default Crystal (dark) theme.</summary>
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
  a { color:#5a47e0 !important; }
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

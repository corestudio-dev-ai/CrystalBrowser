namespace CrystalBrowser.App;

/// <summary>Shared theming helpers for the offline (NavigateToString) pages.</summary>
public static class Theme
{
    /// <summary>
    /// An extra &lt;style&gt; block that re-skins the shared page classes for the light theme.
    /// Returns an empty string for the dark theme (the pages are dark by default).
    /// </summary>
    public static string PageCss(bool light) => light ? """
<style>
  body { background:#f3f2f8 !important; color:#1c1a2e !important; }
  .card,.gauge,.badge,.tile,.steps,.seg,.sw { background:rgba(0,0,0,.04) !important; }
  .card,.gauge,.badge,.tile,.steps { border-color:rgba(0,0,0,.08) !important; }
  .k,.sub,.note,.tag,.greeting,.ust,.lead,.card h2,.badge .d { color:#5b5878 !important; }
  .v,.clock,.tile,.gauge .val,.badge .t { color:#1c1a2e !important; }
  input { background:#fff !important; color:#1c1a2e !important; border-color:rgba(0,0,0,.15) !important; }
  .row,.profrow { border-color:rgba(0,0,0,.08) !important; }
  .seg button { color:#5b5878; }
  a { color:#5a47e0 !important; }
</style>
""" : "";
}

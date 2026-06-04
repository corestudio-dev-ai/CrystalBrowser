namespace CrystalBrowser.App;

/// <summary>
/// The standalone "What's new" / changelog page, rendered offline. Crystal opens this in a tab
/// the first time it runs after updating to a new version; it's also reachable from Settings.
/// When <paramref name="highlight"/> is set, that version is badged as new and the header reads
/// "What's new".
/// </summary>
public static class ChangelogPage
{
    public static string Html(string theme = "dark", string? highlight = null) => $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>What's new — Crystal</title>
<style>
  :root { --accent:#7c6cff; --accent2:#b39bff; color-scheme:dark; }
  * { box-sizing:border-box; margin:0; padding:0; }
  body { font-family:'Segoe UI',system-ui,sans-serif; color:#e9e9ff;
    background:radial-gradient(1100px 650px at 50% -10%, #241f45 0%, #15132a 55%, #0e0d1c 100%);
    min-height:100vh; padding:48px 20px; }
  .wrap { max-width:680px; margin:0 auto; }
  h1 { font-size:30px; font-weight:800; }
  h1 span { background:linear-gradient(90deg,var(--accent),var(--accent2));
    -webkit-background-clip:text; background-clip:text; color:transparent; }
  .tag { color:#a9a6cf; font-size:15px; margin-top:6px; }
  .card { background:rgba(255,255,255,.05); border:1px solid rgba(255,255,255,.08);
    border-radius:16px; padding:8px 24px; margin-top:24px; }
{{Changelog.Css}}
</style>
{{Theme.PageCss(theme)}}
</head>
<body>
<div class="wrap">
  <h1>{{(highlight != null ? "What's <span>new</span>" : "<span>Changelog</span>")}}</h1>
  <div class="tag">{{(highlight != null
      ? $"You're now on Crystal Browser {highlight}. Here's what changed."
      : "Every change to Crystal Browser, newest first.")}}</div>
  <div class="card">
    {{Changelog.EntriesHtml(highlight)}}
  </div>
</div>
</body>
</html>
""";
}

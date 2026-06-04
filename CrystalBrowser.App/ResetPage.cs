namespace CrystalBrowser.App;

/// <summary>
/// Full-screen "Resetting Crystal…" animation shown briefly while settings are restored to
/// defaults and the app restarts (Opera-style). Rendered offline.
/// </summary>
public static class ResetPage
{
    public static string Html(string theme = "dark") => $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Resetting Crystal…</title>
<style>
  :root { --accent:#4f6bff; --accent2:#8aa0ff; color-scheme:dark; }
  * { box-sizing:border-box; margin:0; padding:0; }
  html,body { height:100%; }
  body { font-family:'Segoe UI',system-ui,sans-serif; color:#eef1f8;
    background:radial-gradient(1100px 650px at 50% 30%, #1a2030 0%, #0f131c 55%, #0a0d14 100%);
    display:flex; flex-direction:column; align-items:center; justify-content:center; gap:26px; }
  .gem { width:74px; height:74px; animation:pulse 1.4s ease-in-out infinite; }
  @keyframes pulse {
    0%,100% { transform:scale(1); opacity:.85; filter:drop-shadow(0 0 6px rgba(79,107,255,.5)); }
    50%     { transform:scale(1.12); opacity:1; filter:drop-shadow(0 0 22px rgba(79,107,255,.9)); }
  }
  h1 { font-size:22px; font-weight:600; letter-spacing:.2px; }
  .sub { color:#8b90a3; font-size:14px; margin-top:-14px; }
  .track { width:240px; height:4px; border-radius:3px; background:rgba(255,255,255,.10); overflow:hidden; }
  .track > i { display:block; height:100%; width:40%; border-radius:3px;
    background:linear-gradient(90deg,var(--accent),var(--accent2));
    animation:sweep 1.1s ease-in-out infinite; }
  @keyframes sweep {
    0%   { transform:translateX(-100%); }
    100% { transform:translateX(350%); }
  }
</style>
{{Theme.PageCss(theme)}}
</head>
<body>
  <svg class="gem" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
    <path d="M5 3h14l3 6-10 12L2 9l3-6z" stroke="#8aa0ff" stroke-width="1.4"
          stroke-linejoin="round" fill="rgba(79,107,255,.28)"/></svg>
  <h1>Resetting Crystal</h1>
  <div class="sub">Restoring defaults and restarting…</div>
  <div class="track"><i></i></div>
</body>
</html>
""";
}

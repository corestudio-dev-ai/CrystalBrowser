namespace CrystalBrowser.App;

/// <summary>Pages shown inside a private (Tor) window.</summary>
public static class PrivatePage
{
    /// <summary>
    /// Home / new-tab page for a private window when Tor is up. Reassures the user that the
    /// Tor network is bundled and that everything in this window is routed through it.
    /// </summary>
    public static string PrivateHomeHtml() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Private (Tor) — Crystal</title>
<style>
  :root { color-scheme:dark; --accent:#b39bff; }
  * { box-sizing:border-box; margin:0; padding:0; }
  html,body { height:100%; }
  body { font-family:'Segoe UI',system-ui,sans-serif; color:#e9e9ff;
    background:radial-gradient(1000px 600px at 50% -10%, #2a1f4d 0%, #15132a 55%, #0e0d1c 100%);
    min-height:100%; display:flex; flex-direction:column; align-items:center;
    padding:0 20px; overflow-x:hidden; }
  .shield { margin-top:11vh; font-size:54px; }
  h1 { font-size:30px; font-weight:700; margin-top:8px; }
  h1 span { color:var(--accent); }
  .lead { color:#cbc9ec; font-size:15px; line-height:1.6; margin-top:14px;
    max-width:560px; text-align:center; }
  form { width:min(600px,92vw); margin-top:26px; position:relative; }
  input { width:100%; padding:17px 56px 17px 24px; font-size:16px; color:#fff;
    background:rgba(255,255,255,.06); border:1.5px solid rgba(255,255,255,.12);
    border-radius:30px; outline:none; transition:.2s; }
  input::placeholder { color:#8b88b4; }
  input:focus { border-color:var(--accent); background:rgba(124,108,255,.10);
    box-shadow:0 0 0 4px rgba(124,108,255,.15); }
  .go { position:absolute; right:8px; top:8px; width:38px; height:38px; border:0;
    border-radius:50%; background:#7c6cff; color:#fff; font-size:16px; cursor:pointer; }
  .go:hover { background:#6a5aff; }
  .badges { display:flex; gap:14px; margin-top:30px; flex-wrap:wrap; justify-content:center; }
  .badge { background:rgba(255,255,255,.05); border:1px solid rgba(255,255,255,.08);
    border-radius:14px; padding:14px 18px; width:170px; text-align:left; }
  .badge .t { font-weight:600; font-size:14px; }
  .badge .d { font-size:12px; color:#9a97c4; margin-top:5px; line-height:1.5; }
  .note { font-size:12px; color:#7e7ba6; margin-top:24px; max-width:560px;
    text-align:center; line-height:1.6; }
</style>
</head>
<body>
  <div class="shield">🛡</div>
  <h1>You're browsing <span>privately</span></h1>
  <p class="lead">The <b>Tor network is bundled right inside Crystal Browser</b> — nothing to
     install. Every page in this window is routed through Tor, so your browsing stays
     completely private and your real IP address is never exposed to the sites you visit.</p>

  <form action="https://www.google.com/search" method="get">
    <input name="q" autofocus autocomplete="off" placeholder="Search the web privately…">
    <button class="go" type="submit">&#10148;</button>
  </form>

  <div class="badges">
    <div class="badge"><div class="t">🧅 Bundled Tor</div>
      <div class="d">Crystal ships with Tor — private windows work with zero setup.</div></div>
    <div class="badge"><div class="t">🔒 No IP leaks</div>
      <div class="d">DNS and traffic both tunnel through Tor, so nothing escapes the network.</div></div>
    <div class="badge"><div class="t">🗑 Nothing kept</div>
      <div class="d">This window uses a throwaway profile that's wiped when you close it.</div></div>
  </div>

  <p class="note">Because traffic hops across the Tor network, pages can take a little longer
     to load — the loading bar above shows the progress.</p>
</body>
</html>
""";


    /// <summary>Shown when a private window opens but the Tor proxy isn't reachable.</summary>
    public static string TorMissingHtml() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Private (Tor) — Crystal</title>
<style>
  :root { color-scheme:dark; }
  * { box-sizing:border-box; margin:0; padding:0; }
  body { font-family:'Segoe UI',system-ui,sans-serif; color:#e9e9ff;
    background:radial-gradient(900px 520px at 50% -10%, #2a1f4d 0%, #15132a 55%, #0e0d1c 100%);
    min-height:100vh; display:flex; align-items:center; justify-content:center; padding:24px; }
  .card { max-width:560px; text-align:center; }
  .shield { font-size:52px; }
  h1 { font-size:26px; margin-top:10px; }
  h1 span { color:#b39bff; }
  p { color:#bbbad6; font-size:15px; line-height:1.6; margin-top:14px; }
  .steps { text-align:left; background:rgba(255,255,255,.05); border:1px solid rgba(255,255,255,.08);
    border-radius:14px; padding:18px 22px; margin-top:22px; }
  .steps li { margin:8px 0; color:#d9d8f5; font-size:14px; }
  code { background:rgba(124,108,255,.20); padding:2px 7px; border-radius:6px; }
  a { color:#b39bff; }
  .note { font-size:12px; color:#7e7ba6; margin-top:18px; }
</style>
</head>
<body>
  <div class="card">
    <div class="shield">🛡</div>
    <h1>Private window — <span>Tor not detected</span></h1>
    <p>This window is ready to route all browsing through the <b>Tor network</b> for real
       anonymity, but Tor isn't running on this PC yet.</p>
    <div class="steps">
      <ol>
        <li>Install <b>Tor</b> — the easiest is the
            <a href="https://www.torproject.org/download/">Tor Browser</a>, or the
            <a href="https://www.torproject.org/download/tor/">Tor Expert Bundle</a>.</li>
        <li>Start it so the SOCKS proxy is listening on <code>127.0.0.1:9050</code>
            (or <code>9150</code> for Tor Browser).</li>
        <li>Close this window and open a new <b>Private (Tor)</b> window again.</li>
      </ol>
    </div>
    <p class="note">Until Tor is running, pages in this window won't load — that's by design,
       so nothing leaks outside Tor.</p>
  </div>
</body>
</html>
""";
}

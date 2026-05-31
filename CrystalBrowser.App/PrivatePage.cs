namespace CrystalBrowser.App;

/// <summary>Pages shown inside a private (Tor) window.</summary>
public static class PrivatePage
{
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

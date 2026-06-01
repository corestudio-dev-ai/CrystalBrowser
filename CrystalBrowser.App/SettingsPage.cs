namespace CrystalBrowser.App;

/// <summary>
/// The Crystal Browser Settings / About page. Rendered offline.
/// </summary>
public static class SettingsPage
{
    public const string Version = Config.Version;

    public static string Html() => $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Settings — Crystal</title>
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
  .card { background:rgba(255,255,255,.05); border:1px solid rgba(255,255,255,.08);
    border-radius:16px; padding:22px 24px; margin-top:22px; }
  .card h2 { font-size:14px; text-transform:uppercase; letter-spacing:1.2px;
    color:#9a97c4; margin-bottom:14px; }
  .row { display:flex; justify-content:space-between; padding:9px 0;
    border-bottom:1px solid rgba(255,255,255,.06); font-size:15px; }
  .row:last-child { border-bottom:0; }
  .row .k { color:#b9b7da; } .row .v { color:#fff; font-weight:600; }
  .logo { display:flex; align-items:center; gap:14px; }
  .gem { width:42px; height:42px; }
  .tag { color:#a9a6cf; font-size:15px; margin-top:4px; }
  .note { font-size:13px; color:#7e7ba6; margin-top:10px; }
  a { color:var(--accent2); }
  .btn { background:var(--accent); color:#fff; border:0; border-radius:10px;
    padding:10px 18px; font-size:14px; cursor:pointer; transition:.15s; }
  .btn:hover { background:#6a5aff; }
  .btn:disabled { opacity:.55; cursor:default; }
  .upd { display:flex; align-items:center; gap:14px; margin-top:6px; }
  .ust { font-size:13px; color:#9a97c4; }
</style>
</head>
<body>
<div class="wrap">
  <div class="logo">
    <svg class="gem" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M5 3h14l3 6-10 12L2 9l3-6z" stroke="#b39bff" stroke-width="1.4"
            stroke-linejoin="round" fill="rgba(124,108,255,.25)"/></svg>
    <div>
      <h1>Crystal<span>Browser</span></h1>
      <div class="tag">A fast, dark, Chromium-based web browser.</div>
    </div>
  </div>

  <div class="card">
    <h2>About</h2>
    <div class="row"><span class="k">Version</span><span class="v">{{Version}}</span></div>
    <div class="row"><span class="k">Engine</span><span class="v">WebView2 (Chromium)</span></div>
    <div class="row"><span class="k">Framework</span><span class="v">C# / .NET 8 · WPF</span></div>
    <div class="row"><span class="k">Search</span><span class="v">Google</span></div>
  </div>

  <div class="card">
    <h2>Updates</h2>
    <div class="row"><span class="k">Automatic checks</span><span class="v">Every 60 seconds</span></div>
    <div class="upd">
      <button id="chk" class="btn" onclick="checkUpdates()">Check for updates</button>
      <span id="ust" class="ust"></span>
    </div>
    <div class="note">Crystal checks GitHub automatically while it's open and shows a banner
      when a new version is ready. Use this button to check right now.</div>
  </div>

  <div class="card">
    <h2>Features</h2>
    <div class="row"><span class="k">Tabs</span><span class="v">Vertical</span></div>
    <div class="row"><span class="k">Ad blocker</span><span class="v">uBlock Origin Lite (built-in)</span></div>
    <div class="row"><span class="k">Incognito</span><span class="v">Tor network (bundled)</span></div>
    <div class="row"><span class="k">Edit mode</span><span class="v">document.designMode toggle</span></div>
    <div class="row"><span class="k">System monitor</span><span class="v">Live CPU &amp; RAM</span></div>
  </div>

  <div class="card">
    <h2>Credits</h2>
    <div class="note">Built with C# / .NET 8, WPF and the Microsoft Edge WebView2 runtime.
    Searches the live web via Google.</div>
  </div>
</div>
<script>
  function checkUpdates(){
    var b=document.getElementById('chk'), u=document.getElementById('ust');
    if(window.chrome && window.chrome.webview){
      b.disabled=true; u.textContent='Checking…';
      window.chrome.webview.postMessage('check-updates');
    } else {
      u.textContent='Update checks are unavailable here.';
    }
  }
  // Called from the host app with the result of a check.
  window.crystalUpdateStatus=function(state,ver,page,dl){
    var b=document.getElementById('chk'), u=document.getElementById('ust');
    if(state==='checking'){ b.disabled=true; u.textContent='Checking…'; return; }
    if(state==='available'){
      b.disabled=true;
      u.innerHTML='Update available: <b>'+ver+'</b> — <a href="'+(dl||page)+'">direct download link</a>';
      return;
    }
    if(state==='downloading'){
      b.disabled=true;
      u.textContent='Downloading '+ver+'… the installer will open automatically.';
      return;
    }
    b.disabled=false;
    if(state==='failed'){
      u.innerHTML='Automatic download failed — <a href="'+(page||'#')+'">open the release page</a> to update manually.';
    } else if(state==='current'){
      u.textContent="You're on the latest version.";
    } else { u.textContent=''; }
  };
</script>
</body>
</html>
""";
}

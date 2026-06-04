namespace CrystalBrowser.App;

/// <summary>
/// The Crystal Browser Settings / About page. Rendered offline. Controls talk to the host
/// app over the WebView2 message bridge (getSettings / setTabLayout / setStartup / setAccent /
/// addProfile / switchProfile) so changes actually take effect and persist.
/// </summary>
public static class SettingsPage
{
    public const string Version = Config.Version;

    // A JS array literal of the selectable themes, e.g. [{key:'dark',name:'Crystal'},…]
    private static string ThemesJs =>
        "[" + string.Join(",", Theme.All.Select(t => $"{{key:'{t.Key}',name:'{t.Name}'}}")) + "]";

    public static string Html(string theme = "dark") => $$"""
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
  .row { display:flex; justify-content:space-between; align-items:center; padding:11px 0;
    border-bottom:1px solid rgba(255,255,255,.06); font-size:15px; gap:16px; }
  .row:last-child { border-bottom:0; }
  .row .k { color:#b9b7da; } .row .v { color:#fff; font-weight:600; }
  .sub { font-size:12px; color:#7e7ba6; margin-top:3px; }
  .logo { display:flex; align-items:center; gap:14px; }
  .gem { width:42px; height:42px; }
  .tag { color:#a9a6cf; font-size:15px; margin-top:4px; }
  .note { font-size:13px; color:#7e7ba6; margin-top:10px; }
  a { color:var(--accent2); }
  .btn { background:var(--accent); color:#fff; border:0; border-radius:10px;
    padding:10px 18px; font-size:14px; cursor:pointer; transition:.15s; }
  .btn:hover { filter:brightness(1.1); }
  .btn:disabled { opacity:.55; cursor:default; }
  .ghost { background:rgba(255,255,255,.08); }
  .upd { display:flex; align-items:center; gap:14px; margin-top:6px; }
  .ust { font-size:13px; color:#9a97c4; }
  /* segmented toggle */
  .seg { display:inline-flex; background:rgba(255,255,255,.06); border-radius:10px; padding:3px; }
  .seg button { background:transparent; color:#cfceeb; border:0; padding:8px 16px;
    border-radius:8px; font-size:13px; cursor:pointer; }
  .seg button.on { background:var(--accent); color:#fff; }
  /* accent swatches */
  .swatches { display:flex; gap:10px; }
  .sw { width:28px; height:28px; border-radius:50%; cursor:pointer; border:2px solid transparent; }
  .sw.on { border-color:#fff; box-shadow:0 0 0 2px rgba(255,255,255,.25); }
  /* inputs */
  input[type=text], input[type=url] { background:rgba(255,255,255,.06); color:#fff;
    border:1px solid rgba(255,255,255,.14); border-radius:9px; padding:9px 12px; font-size:14px;
    outline:none; min-width:240px; }
  input:focus { border-color:var(--accent); }
  label.opt { display:flex; align-items:center; gap:10px; padding:7px 0; color:#d9d8f5; font-size:14px; cursor:pointer; }
  .profrow { display:flex; align-items:center; justify-content:space-between; padding:9px 0;
    border-bottom:1px solid rgba(255,255,255,.06); }
  .profrow:last-child { border-bottom:0; }
  .pill { font-size:11px; color:#cdbcff; background:rgba(124,108,255,.22);
    padding:2px 9px; border-radius:9px; margin-left:8px; }
  /* Chrome-style profile hero card */
  .profhero { background:rgba(124,108,255,.10); border:1px solid rgba(124,108,255,.28);
    border-radius:18px; padding:24px 22px 0; text-align:center; overflow:hidden; }
  .avatar { width:64px; height:64px; border-radius:50%; margin:0 auto 12px;
    background:linear-gradient(135deg,var(--accent),var(--accent2)); color:#fff;
    font-size:28px; font-weight:700; display:flex; align-items:center; justify-content:center; }
  .phname { font-size:18px; font-weight:700; }
  .phsync { font-size:13px; color:#9a97c4; margin-top:3px; }
  .phbar { margin-top:18px; padding:14px; border-top:1px solid rgba(255,255,255,.12); }
</style>
{{Theme.PageCss(theme)}}
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
    <h2>Appearance</h2>
    <div class="row">
      <div><div class="k">Tabs</div><div class="sub">Choose where your tabs live.</div></div>
      <div class="seg" id="segTabs">
        <button data-v="horizontal" onclick="setTabs('horizontal')">Horizontal</button>
        <button data-v="vertical" onclick="setTabs('vertical')">Vertical</button>
      </div>
    </div>
    <div class="row">
      <div><div class="k">Theme</div><div class="sub">Each theme sets a colour and accent.</div></div>
      <div class="seg" id="segTheme"></div>
    </div>
    <div class="row">
      <div><div class="k">Accent colour</div><div class="sub">Fine-tune the accent on top of your theme.</div></div>
      <div class="swatches" id="swatches"></div>
    </div>
    <div class="row">
      <div><div class="k">Window frame</div><div class="sub">Colour of the window's border.</div></div>
      <div class="swatches" id="frameSwatches"></div>
    </div>
  </div>

  <div class="card">
    <h2>Search</h2>
    <div class="row">
      <div><div class="k">Search engine</div><div class="sub">Used by the address bar and new-tab search.</div></div>
      <div class="seg" id="segEngine">
        <button data-v="google" onclick="setEngine('google')">Google</button>
        <button data-v="duckduckgo" onclick="setEngine('duckduckgo')">DuckDuckGo</button>
      </div>
    </div>
  </div>

  <div class="card">
    <h2>On startup</h2>
    <label class="opt"><input type="radio" name="su" value="newtab" onchange="setStartup()"> Open the New Tab page</label>
    <label class="opt"><input type="radio" name="su" value="url" onchange="setStartup()"> Open a specific page</label>
    <div class="row" style="border:0;">
      <input type="url" id="suUrl" placeholder="https://example.com" oninput="setStartup()">
    </div>
  </div>

  <div class="card">
    <h2>Crystal profiles</h2>
    <div class="profhero">
      <div class="avatar" id="avatar">D</div>
      <div class="phname" id="phname">Default</div>
      <div class="phsync" id="phsync">Not signed in</div>
      <div class="phbar">
        <button class="btn" id="signBtn" onclick="toggleSign()">Sign in to sync</button>
      </div>
    </div>
    <div class="sub" style="margin:16px 0 8px;">Each profile keeps its own separate cookies,
      logins, history and bookmarks. Switching reopens the window in that profile.</div>
    <div id="profiles"></div>
    <div class="row" style="border:0;">
      <input type="text" id="newProf" placeholder="New profile name">
      <button class="btn ghost" onclick="addProfile()">Add profile</button>
    </div>
  </div>

  <div class="card">
    <h2>Updates</h2>
    <div class="row"><span class="k">Automatic checks</span><span class="v">Every 60 seconds</span></div>
    <div class="upd">
      <button id="chk" class="btn" onclick="checkUpdates()">Check for updates</button>
      <span id="ust" class="ust"></span>
    </div>
    <div class="note">Crystal checks GitHub automatically while it's open and shows a banner
      when a new version is ready. Use this button to check (and install) right now.</div>
  </div>

  <div class="card">
    <h2>What's new</h2>
    <div class="row" style="border:0;">
      <div><div class="k">Changelog</div><div class="sub">See what changed in each version of Crystal.</div></div>
      <button class="btn" onclick="send({type:'openChangelog'})">View changelog</button>
    </div>
  </div>

  <div class="card">
    <h2>Reset</h2>
    <div class="row" style="border:0;">
      <div><div class="k">Reset Crystal</div><div class="sub">Restore all settings to their defaults and restart. Your bookmarks and history are kept.</div></div>
      <button class="btn ghost" onclick="resetCrystal()">Reset settings</button>
    </div>
  </div>

  <div class="card">
    <h2>About</h2>
    <div class="row"><span class="k">Version</span><span class="v">{{Version}}</span></div>
    <div class="row"><span class="k">Engine</span><span class="v">WebView2 (Chromium)</span></div>
    <div class="row"><span class="k">Ad blocker</span><span class="v">uBlock Origin Lite (built-in)</span></div>
    <div class="row"><span class="k">Search</span><span class="v" id="aboutSearch">Google</span></div>
  </div>
</div>
<script>
  var ACCENTS = ['#4F6BFF','#00B4D8','#7C6CFF','#22C55E','#FF6E40','#FF3D7E'];
  var FRAMES = ['#4F6BFF','#2B2F3A','#00B4D8','#7C6CFF','#22C55E','#FF3D7E'];
  var THEMES = {{ThemesJs}};
  function send(o){ if(window.chrome && window.chrome.webview) window.chrome.webview.postMessage(JSON.stringify(o)); }

  function setTabs(v){ send({type:'setTabLayout', value:v}); markTabs(v); }
  function setTheme(v){ send({type:'setTheme', value:v}); markTheme(v); }
  function setEngine(v){ send({type:'setSearchEngine', value:v}); markEngine(v); }
  function setFrame(v){ send({type:'setFrameColor', value:v}); markFrame(v); }
  function resetCrystal(){
    if(confirm('Reset all Crystal settings to their defaults and restart? Your bookmarks and history are kept.'))
      send({type:'resetSettings'});
  }
  function setAccent(v){ send({type:'setAccent', value:v}); markAccent(v);
    document.documentElement.style.setProperty('--accent', v); }
  function setStartup(){
    var mode = document.querySelector('input[name=su]:checked');
    mode = mode ? mode.value : 'newtab';
    send({type:'setStartup', mode:mode, url:document.getElementById('suUrl').value});
  }
  function addProfile(){
    var n = document.getElementById('newProf').value.trim();
    if(n){ send({type:'addProfile', name:n}); document.getElementById('newProf').value=''; }
  }
  function useProfile(n){ send({type:'switchProfile', name:n}); }
  var ACCOUNT=null;
  function toggleSign(){
    if(ACCOUNT){ send({type:'signOut'}); }
    else { var e=prompt('Sign in to sync — enter your email:'); if(e && e.trim()) send({type:'signIn', email:e.trim()}); }
  }

  function markTabs(v){
    document.querySelectorAll('#segTabs button').forEach(function(b){
      b.classList.toggle('on', b.dataset.v===v); });
  }
  function markTheme(v){
    document.querySelectorAll('#segTheme button').forEach(function(b){
      b.classList.toggle('on', b.dataset.v===v); });
  }
  function markEngine(v){
    document.querySelectorAll('#segEngine button').forEach(function(b){
      b.classList.toggle('on', b.dataset.v===v); });
    document.getElementById('aboutSearch').textContent = v==='duckduckgo' ? 'DuckDuckGo' : 'Google';
  }
  function buildThemes(){
    var box=document.getElementById('segTheme'); box.innerHTML='';
    THEMES.forEach(function(t){
      var b=document.createElement('button'); b.dataset.v=t.key; b.textContent=t.name;
      b.onclick=function(){ setTheme(t.key); };
      box.appendChild(b);
    });
  }
  function markAccent(v){
    document.querySelectorAll('#swatches .sw').forEach(function(s){
      s.classList.toggle('on', (s.dataset.v||'').toUpperCase()===(v||'').toUpperCase()); });
  }
  function buildSwatches(){
    var box=document.getElementById('swatches');
    box.innerHTML='';
    ACCENTS.forEach(function(c){
      var d=document.createElement('div'); d.className='sw'; d.style.background=c; d.dataset.v=c;
      d.title=c; d.onclick=function(){ setAccent(c); };
      box.appendChild(d);
    });
  }
  function markFrame(v){
    document.querySelectorAll('#frameSwatches .sw').forEach(function(s){
      s.classList.toggle('on', (s.dataset.v||'').toUpperCase()===(v||'').toUpperCase()); });
  }
  function buildFrameSwatches(){
    var box=document.getElementById('frameSwatches');
    box.innerHTML='';
    FRAMES.forEach(function(c){
      var d=document.createElement('div'); d.className='sw'; d.style.background=c; d.dataset.v=c;
      d.title=c; d.onclick=function(){ setFrame(c); };
      box.appendChild(d);
    });
  }

  // Host pushes the current settings here.
  window.crystalSettings = function(s){
    markTabs(s.tabLayout);
    markTheme(s.theme||'dark');
    markEngine(s.searchEngine||'google');
    markAccent(s.accent);
    markFrame(s.frameColor||'#4F6BFF');
    document.documentElement.style.setProperty('--accent', s.accent);
    var r=document.querySelector('input[name=su][value="'+(s.startup||'newtab')+'"]'); if(r) r.checked=true;
    document.getElementById('suUrl').value = s.startupUrl||'';
    // Chrome-like profile hero
    ACCOUNT = s.account || null;
    var initial = (ACCOUNT || s.activeProfile || 'D').trim().charAt(0).toUpperCase();
    document.getElementById('avatar').textContent = initial || 'D';
    document.getElementById('phname').textContent = s.activeProfile || 'Default';
    document.getElementById('phsync').textContent = ACCOUNT ? ('Synced as '+ACCOUNT) : 'Not signed in';
    document.getElementById('signBtn').textContent = ACCOUNT ? 'Sign out' : 'Sign in to sync';
    var box=document.getElementById('profiles'); box.innerHTML='';
    (s.profiles||['Default']).forEach(function(p){
      var row=document.createElement('div'); row.className='profrow';
      var active = (p===s.activeProfile);
      var left=document.createElement('div'); left.innerHTML='<span class="v">'+p+'</span>'+(active?'<span class="pill">Active</span>':'');
      var btn=document.createElement('button'); btn.className='btn ghost'; btn.textContent= active?'In use':'Use';
      btn.disabled=active; btn.onclick=function(){ useProfile(p); };
      row.appendChild(left); row.appendChild(btn); box.appendChild(row);
    });
  };

  function checkUpdates(){
    var b=document.getElementById('chk'), u=document.getElementById('ust');
    if(window.chrome && window.chrome.webview){
      b.disabled=true; u.textContent='Checking…';
      window.chrome.webview.postMessage('check-updates');
    } else { u.textContent='Update checks are unavailable here.'; }
  }
  window.crystalUpdateStatus=function(state,ver,page,dl){
    var b=document.getElementById('chk'), u=document.getElementById('ust');
    if(state==='checking'){ b.disabled=true; u.textContent='Checking…'; return; }
    if(state==='available'){ b.disabled=true;
      u.innerHTML='Update available: <b>'+ver+'</b> — <a href="'+(dl||page)+'">direct download link</a>'; return; }
    if(state==='downloading'){ b.disabled=true;
      u.textContent='Downloading '+ver+'… the installer will open automatically.'; return; }
    b.disabled=false;
    if(state==='failed'){ u.innerHTML='Automatic download failed — <a href="'+(page||'#')+'">open the release page</a> to update manually.'; }
    else if(state==='error'){ u.textContent="Couldn't reach the update server. Check your connection and try again."; }
    else if(state==='current'){ u.textContent="You're on the latest version."; }
    else { u.textContent=''; }
  };

  buildThemes();
  buildSwatches();
  buildFrameSwatches();
  send({type:'getSettings'});
</script>
</body>
</html>
""";
}

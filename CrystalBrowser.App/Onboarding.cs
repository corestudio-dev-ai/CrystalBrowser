namespace CrystalBrowser.App;

/// <summary>
/// First-run onboarding / welcome flow, shown the first time Crystal opens (a normal,
/// non-incognito window) until it's completed. Rendered offline via NavigateToString. A
/// stepper walks the user through the headline features, then lets them pick a theme and a
/// search engine, import bookmarks from an installed browser, and set Crystal as default.
/// Talks to the host over the WebView2 message bridge (see <c>HandleSettingsMessage</c>).
/// </summary>
public static class Onboarding
{
    public static string Html(string theme) => $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Welcome to Crystal</title>
<style>
  :root { --accent:#7c6cff; --accent2:#b39bff; color-scheme:dark; }
  * { box-sizing:border-box; margin:0; padding:0; }
  body { font-family:'Segoe UI',system-ui,sans-serif; color:#e9e9ff;
    background:radial-gradient(1100px 650px at 50% -10%, #241f45 0%, #15132a 55%, #0e0d1c 100%);
    min-height:100vh; display:flex; align-items:center; justify-content:center; padding:40px 20px; }
  .card { width:min(620px,94vw); background:rgba(255,255,255,.05);
    border:1px solid rgba(255,255,255,.10); border-radius:22px; padding:34px 36px; }
  .logo { font-size:30px; font-weight:800; letter-spacing:-1px; }
  .logo span { background:linear-gradient(90deg,var(--accent),var(--accent2));
    -webkit-background-clip:text; background-clip:text; color:transparent; }
  h2 { font-size:23px; font-weight:700; margin-top:6px; }
  .lead { color:#a9a6cf; font-size:15px; line-height:1.6; margin-top:10px; }
  .feat { display:flex; gap:14px; align-items:flex-start; padding:13px 0;
    border-bottom:1px solid rgba(255,255,255,.06); }
  .feat:last-child { border-bottom:0; }
  .feat .ic { font-size:22px; width:30px; text-align:center; }
  .feat .t { font-weight:600; font-size:15px; }
  .feat .d { color:#9a97c4; font-size:13px; margin-top:2px; line-height:1.5; }
  .opts { display:flex; gap:12px; flex-wrap:wrap; margin-top:20px; }
  .opt { flex:1 1 120px; border:1.5px solid rgba(255,255,255,.12); border-radius:14px;
    padding:14px; cursor:pointer; background:rgba(255,255,255,.04); transition:.15s; text-align:center; }
  .opt:hover { border-color:var(--accent); }
  .opt.on { border-color:var(--accent); background:rgba(124,108,255,.16);
    box-shadow:0 0 0 3px rgba(124,108,255,.18); }
  .opt .sw { width:40px; height:40px; border-radius:10px; margin:0 auto 9px; }
  .opt .nm { font-size:14px; font-weight:600; }
  .src { display:flex; align-items:center; justify-content:space-between; gap:12px;
    padding:12px 14px; border:1px solid rgba(255,255,255,.10); border-radius:12px; margin-top:10px; }
  .src .nm { font-weight:600; font-size:14px; }
  .src .st { font-size:12px; color:#9a97c4; }
  .nav { display:flex; justify-content:space-between; align-items:center; margin-top:28px; }
  .dots { display:flex; gap:7px; }
  .dot { width:8px; height:8px; border-radius:50%; background:rgba(255,255,255,.18); }
  .dot.on { background:var(--accent); }
  .btn { background:var(--accent); color:#fff; border:0; border-radius:11px;
    padding:11px 22px; font-size:15px; font-weight:600; cursor:pointer; transition:.15s; }
  .btn:hover { filter:brightness(1.1); }
  .btn:disabled { opacity:.5; cursor:default; }
  .ghost { background:rgba(255,255,255,.08); font-weight:500; }
  .step { display:none; }
  .step.on { display:block; }
</style>
<style id="themecss">{{Theme.PageCssInner(theme)}}</style>
</head>
<body>
<div class="card">
  <div class="logo">Crystal<span>Browser</span></div>

  <!-- 1. Welcome -->
  <div class="step on" data-step="0">
    <h2>Welcome to Crystal</h2>
    <p class="lead">A fast, private, Chromium-based browser. Here's what comes built in:</p>
    <div style="margin-top:14px;">
      <div class="feat"><div class="ic">🛡️</div><div><div class="t">Ad &amp; tracker blocking</div>
        <div class="d">uBlock Origin Lite is built in and on by default — no setup.</div></div></div>
      <div class="feat"><div class="ic">👤</div><div><div class="t">Crystal profiles</div>
        <div class="d">Keep work and personal browsing fully separate, each with its own data.</div></div></div>
      <div class="feat"><div class="ic">🕶️</div><div><div class="t">Incognito windows</div>
        <div class="d">Throwaway sessions that wipe themselves when you close them.</div></div></div>
      <div class="feat"><div class="ic">📊</div><div><div class="t">Live system monitor</div>
        <div class="d">Your new-tab page shows real-time CPU and memory usage.</div></div></div>
    </div>
  </div>

  <!-- 2. Theme -->
  <div class="step" data-step="1">
    <h2>Pick a look</h2>
    <p class="lead">Choose a theme. You can change it any time in Settings.</p>
    <div class="opts" id="themeOpts"></div>
  </div>

  <!-- 3. Search engine -->
  <div class="step" data-step="2">
    <h2>Choose your search engine</h2>
    <p class="lead">Used for the address bar and your new-tab search box.</p>
    <div class="opts" id="engineOpts">
      <div class="opt" data-v="google" onclick="pickEngine('google')">
        <div class="sw" style="background:linear-gradient(135deg,#4285F4,#34A853 60%,#FBBC05)"></div>
        <div class="nm">Google</div></div>
      <div class="opt" data-v="duckduckgo" onclick="pickEngine('duckduckgo')">
        <div class="sw" style="background:linear-gradient(135deg,#de5833,#f59e6b)"></div>
        <div class="nm">DuckDuckGo</div></div>
    </div>
  </div>

  <!-- 4. Import bookmarks -->
  <div class="step" data-step="3">
    <h2>Bring your bookmarks</h2>
    <p class="lead">Import bookmarks from a browser already installed on this PC.</p>
    <div id="sources"></div>
    <p class="lead" id="noSources" style="display:none;">No other browsers with bookmarks were found — you can skip this.</p>
  </div>

  <!-- 5. Default browser -->
  <div class="step" data-step="4">
    <h2>Make Crystal your default</h2>
    <p class="lead">Set Crystal as your default browser so links open here. Windows will ask you
      to confirm in its Settings app.</p>
    <button class="btn" style="margin-top:18px;" onclick="setDefault()">Set as default browser</button>
  </div>

  <!-- 6. Done -->
  <div class="step" data-step="5">
    <h2>You're all set ✨</h2>
    <p class="lead">Crystal is ready to go. Tweak anything later from the Settings page.</p>
  </div>

  <div class="nav">
    <button class="btn ghost" id="back" onclick="go(-1)">Back</button>
    <div class="dots" id="dots"></div>
    <button class="btn" id="next" onclick="go(1)">Next</button>
  </div>
</div>

<script>
  var STEP=0, STEPS=6, ENGINE='google', THEME='dark';
  function send(o){ if(window.chrome && window.chrome.webview) window.chrome.webview.postMessage(JSON.stringify(o)); }

  function render(){
    document.querySelectorAll('.step').forEach(function(s){
      s.classList.toggle('on', +s.dataset.step===STEP); });
    var dots=document.getElementById('dots'); dots.innerHTML='';
    for(var i=0;i<STEPS;i++){ var d=document.createElement('div');
      d.className='dot'+(i===STEP?' on':''); dots.appendChild(d); }
    document.getElementById('back').style.visibility = STEP===0?'hidden':'visible';
    document.getElementById('next').textContent = STEP===STEPS-1 ? 'Finish' : 'Next';
  }
  function go(d){
    if(STEP===STEPS-1 && d>0){ send({type:'finishOnboarding'}); return; }
    STEP=Math.max(0,Math.min(STEPS-1,STEP+d)); render();
  }

  function pickTheme(k){ THEME=k; send({type:'setTheme', value:k}); markTheme(); }
  function markTheme(){ document.querySelectorAll('#themeOpts .opt').forEach(function(o){
    o.classList.toggle('on', o.dataset.v===THEME); }); }
  function pickEngine(k){ ENGINE=k; send({type:'setSearchEngine', value:k}); markEngine(); }
  function markEngine(){ document.querySelectorAll('#engineOpts .opt').forEach(function(o){
    o.classList.toggle('on', o.dataset.v===ENGINE); }); }
  function importFrom(id,btn){ btn.disabled=true; btn.textContent='Importing…'; send({type:'importBookmarks', source:id}); }
  function setDefault(){ send({type:'setDefaultBrowser'}); }

  // Host pushes initial state here.
  window.crystalOnboard = function(s){
    THEME = s.theme||'dark'; ENGINE = s.engine||'google';
    var box=document.getElementById('themeOpts'); box.innerHTML='';
    (s.themes||[]).forEach(function(t){
      var o=document.createElement('div'); o.className='opt'; o.dataset.v=t.key;
      o.onclick=function(){ pickTheme(t.key); };
      o.innerHTML='<div class="sw" style="background:'+t.swatch+'"></div><div class="nm">'+t.name+'</div>';
      box.appendChild(o);
    });
    markTheme(); markEngine();
    var sb=document.getElementById('sources'); sb.innerHTML='';
    var srcs=s.sources||[];
    document.getElementById('noSources').style.display = srcs.length?'none':'block';
    srcs.forEach(function(src){
      var row=document.createElement('div'); row.className='src';
      var left=document.createElement('div');
      left.innerHTML='<div class="nm">'+src.name+'</div><div class="st" id="st_'+src.id+'">'+src.count+' bookmarks found</div>';
      var btn=document.createElement('button'); btn.className='btn ghost'; btn.textContent='Import';
      btn.onclick=function(){ importFrom(src.id, btn); };
      row.appendChild(left); row.appendChild(btn); sb.appendChild(row);
    });
  };
  // Host reports the result of an import.
  window.crystalImportResult = function(id,count){
    var st=document.getElementById('st_'+id);
    if(st) st.textContent = count+' bookmarks imported';
    var btn = st && st.parentElement.parentElement.querySelector('button');
    if(btn){ btn.textContent='Imported ✓'; btn.disabled=true; }
  };
  // Host hot-swaps the theme CSS for live preview (no reload).
  window.crystalTheme = function(css){ document.getElementById('themecss').textContent = css; };

  render();
  send({type:'getOnboarding'});
</script>
</body>
</html>
""";
}

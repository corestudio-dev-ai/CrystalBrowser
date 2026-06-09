namespace CrystalBrowser.App;

/// <summary>
/// The Crystal Browser home / new-tab page (2.1 AERO). Rendered offline via NavigateToString so
/// it loads instantly. A bright glass design: soft drifting colour blobs behind frosted cards,
/// with staggered entrance animations. The host app pushes live system telemetry into it each
/// second via the global <c>crystalStats()</c> hook.
/// </summary>
public static class HomePage
{
    public static string Html(string theme = "aero", string engine = "google") => $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>New Tab — Crystal</title>
<style>
  :root { --accent:#2F6BFF; --accent2:#8FB6FF; }
  * { box-sizing:border-box; margin:0; padding:0; }
  html,body { height:100%; }
  body {
    font-family:'Segoe UI',system-ui,sans-serif; color:#0f1b2d;
    background:radial-gradient(1100px 700px at 50% -10%, #dfeaff 0%, #eef4ff 45%, #f8fbff 100%);
    min-height:100%; display:flex; flex-direction:column; align-items:center;
    padding:0 20px; overflow-x:hidden; position:relative;
  }

  /* Ambient aero blobs: soft colour orbs drifting slowly behind the glass. */
  .blob { position:fixed; border-radius:50%; filter:blur(70px); opacity:.5;
    pointer-events:none; z-index:-1; }
  .b1 { width:480px; height:480px; left:-120px; top:-140px;
    background:radial-gradient(circle, var(--accent2), transparent 70%);
    animation:drift1 24s ease-in-out infinite alternate; }
  .b2 { width:420px; height:420px; right:-110px; top:22%;
    background:radial-gradient(circle, #b7e0ff, transparent 70%);
    animation:drift2 30s ease-in-out infinite alternate; }
  .b3 { width:380px; height:380px; left:18%; bottom:-160px;
    background:radial-gradient(circle, #d7c9ff, transparent 70%);
    animation:drift3 27s ease-in-out infinite alternate; }
  @keyframes drift1 { to { transform:translate(70px,50px) scale(1.12); } }
  @keyframes drift2 { to { transform:translate(-60px,-70px) scale(1.08); } }
  @keyframes drift3 { to { transform:translate(50px,-50px) scale(1.15); } }

  /* Staggered entrance: everything floats up into place. */
  @keyframes rise { from { opacity:0; transform:translateY(16px); }
                    to   { opacity:1; transform:none; } }
  .clock, .greeting, .logo, form, .sys { animation:rise .55s cubic-bezier(.2,.7,.3,1) both; }
  .greeting { animation-delay:.07s; }
  .logo     { animation-delay:.14s; }
  form      { animation-delay:.21s; }
  .sys      { animation-delay:.30s; }

  .clock { margin-top:9vh; font-size:64px; font-weight:200; letter-spacing:2px; }
  .greeting { font-size:18px; color:#5a6b80; margin-top:4px; }
  .logo { margin-top:6vh; font-size:40px; font-weight:800; letter-spacing:-1px; }
  .logo span { background:linear-gradient(90deg,var(--accent),var(--accent2));
    -webkit-background-clip:text; background-clip:text; color:transparent;
    background-size:200% 100%; animation:shine 6s ease-in-out infinite; }
  @keyframes shine { 0%,100% { background-position:0% 0; } 50% { background-position:100% 0; } }

  form { width:min(640px,92vw); margin-top:22px; position:relative; }
  input {
    width:100%; padding:18px 56px 18px 26px; font-size:17px; color:#0f1b2d;
    background:rgba(255,255,255,.6); border:1.5px solid rgba(15,27,45,.10);
    border-radius:32px; outline:none; backdrop-filter:blur(18px) saturate(1.3);
    box-shadow:0 10px 36px rgba(31,60,110,.08);
    transition:border-color .25s, box-shadow .25s, background .25s, transform .25s;
  }
  input::placeholder { color:#8295ab; }
  input:focus { border-color:var(--accent); background:rgba(255,255,255,.82);
    box-shadow:0 0 0 5px rgba(47,107,255,.12), 0 12px 40px rgba(31,60,110,.12);
    transform:translateY(-1px); }
  .go { position:absolute; right:8px; top:8px; width:40px; height:40px; border:0;
    border-radius:50%; background:var(--accent); color:#fff; font-size:17px; cursor:pointer;
    transition:transform .18s, background .18s; }
  .go:hover { background:#1f56e8; transform:scale(1.08); }
  .go:active { transform:scale(.94); }

  .sys {
    margin-top:auto; margin-bottom:26px; width:min(640px,92vw);
    display:flex; gap:14px; padding-top:30px;
  }
  .gauge { flex:1; background:rgba(255,255,255,.55); border:1px solid rgba(15,27,45,.08);
    border-radius:16px; padding:14px 16px; backdrop-filter:blur(18px) saturate(1.3);
    box-shadow:0 8px 30px rgba(31,60,110,.06);
    transition:transform .25s, box-shadow .25s; }
  .gauge:hover { transform:translateY(-3px); box-shadow:0 14px 40px rgba(31,60,110,.10); }
  .gauge .lab { font-size:12px; color:#5a6b80; display:flex; justify-content:space-between; }
  .gauge .val { font-size:22px; font-weight:600; margin:6px 0 9px; color:#0f1b2d; }
  .bar { height:7px; border-radius:4px; background:rgba(15,27,45,.08); overflow:hidden; }
  .bar > i { display:block; height:100%; width:0%; border-radius:4px;
    background:linear-gradient(90deg,var(--accent),var(--accent2)); transition:width .5s; }
  .sub { font-size:11px; color:#7c8da1; margin-top:6px; }
</style>
{{Theme.PageCss(theme)}}
</head>
<body>
  <div class="blob b1"></div><div class="blob b2"></div><div class="blob b3"></div>

  <div class="clock" id="clock">--:--</div>
  <div class="greeting" id="greet">Welcome to Crystal</div>

  <div class="logo">Crystal <span>AERO</span></div>

  <form action="{{Config.SearchFormAction(engine)}}" method="get">
    <input name="q" autofocus autocomplete="off" placeholder="Search {{Config.SearchName(engine)}} or type a URL…">
    <button class="go" type="submit">&#10148;</button>
  </form>

  <div class="sys">
    <div class="gauge">
      <div class="lab"><span>CPU</span><span id="cpuCores"></span></div>
      <div class="val" id="cpuVal">—</div>
      <div class="bar"><i id="cpuBar"></i></div>
    </div>
    <div class="gauge">
      <div class="lab"><span>Memory</span><span id="ramDetail"></span></div>
      <div class="val" id="ramVal">—</div>
      <div class="bar"><i id="ramBar"></i></div>
      <div class="sub" id="appMem"></div>
    </div>
  </div>

<script>
  // Live clock + greeting, updated locally.
  function tick(){
    var d=new Date();
    document.getElementById('clock').textContent =
      d.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'});
    var h=d.getHours();
    var g = h<12 ? 'Good morning' : h<18 ? 'Good afternoon' : 'Good evening';
    document.getElementById('greet').textContent = g + ' — welcome to Crystal';
  }
  tick(); setInterval(tick, 10000);

  // Hook the host app calls every second with real telemetry.
  window.crystalStats = function(s){
    document.getElementById('cpuVal').textContent = s.cpu.toFixed(0) + '%';
    document.getElementById('cpuBar').style.width = s.cpu + '%';
    document.getElementById('cpuCores').textContent = s.cores + ' cores';
    document.getElementById('ramVal').textContent = s.ramPct.toFixed(0) + '%';
    document.getElementById('ramBar').style.width = s.ramPct + '%';
    document.getElementById('ramDetail').textContent =
      s.ramUsed.toFixed(1) + ' / ' + s.ramTotal.toFixed(1) + ' GB';
    document.getElementById('appMem').textContent =
      'Crystal is using ' + s.appMem.toFixed(0) + ' MB';
  };
</script>
</body>
</html>
""";
}

namespace CrystalBrowser.App;

/// <summary>
/// The Crystal Browser home / new-tab page. Rendered offline via NavigateToString so it
/// loads instantly. Its search box posts to Google, and the host app pushes live system
/// telemetry into it each second via the global <c>crystalStats()</c> hook.
/// </summary>
public static class HomePage
{
    public static string Html(string theme = "dark", string engine = "google") => $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>New Tab — Crystal</title>
<style>
  :root { --accent:#7c6cff; --accent2:#b39bff; }
  * { box-sizing:border-box; margin:0; padding:0; }
  html,body { height:100%; }
  body {
    font-family:'Segoe UI',system-ui,sans-serif; color:#e9e9ff;
    background:radial-gradient(1200px 700px at 50% -10%, #2a2350 0%, #16142b 55%, #0e0d1c 100%);
    min-height:100%; display:flex; flex-direction:column; align-items:center;
    padding:0 20px; overflow-x:hidden;
  }
  .clock { margin-top:9vh; font-size:64px; font-weight:200; letter-spacing:2px; }
  .greeting { font-size:18px; color:#a9a6cf; margin-top:4px; }
  .logo { margin-top:6vh; font-size:40px; font-weight:800; letter-spacing:-1px; }
  .logo span { background:linear-gradient(90deg,var(--accent),var(--accent2));
    -webkit-background-clip:text; background-clip:text; color:transparent; }
  form { width:min(640px,92vw); margin-top:22px; position:relative; }
  input {
    width:100%; padding:18px 56px 18px 26px; font-size:17px; color:#fff;
    background:rgba(255,255,255,.06); border:1.5px solid rgba(255,255,255,.12);
    border-radius:32px; outline:none; transition:.2s; backdrop-filter:blur(8px);
  }
  input::placeholder { color:#8b88b4; }
  input:focus { border-color:var(--accent); background:rgba(124,108,255,.10);
    box-shadow:0 0 0 4px rgba(124,108,255,.15); }
  .go { position:absolute; right:8px; top:8px; width:40px; height:40px; border:0;
    border-radius:50%; background:var(--accent); color:#fff; font-size:17px; cursor:pointer; }
  .go:hover { background:#6a5aff; }
  .tiles { display:flex; gap:14px; margin-top:30px; flex-wrap:wrap; justify-content:center; }
  .tile { width:96px; height:84px; border-radius:16px; background:rgba(255,255,255,.05);
    border:1px solid rgba(255,255,255,.08); display:flex; flex-direction:column; gap:8px;
    align-items:center; justify-content:center; text-decoration:none; color:#d9d8f5;
    font-size:13px; transition:.15s; cursor:pointer; }
  .tile:hover { background:rgba(124,108,255,.18); transform:translateY(-2px); }
  .tile img { width:30px; height:30px; border-radius:7px; }
  .tile .ico { width:30px; height:30px; }
  .sys {
    margin-top:auto; margin-bottom:26px; width:min(640px,92vw);
    display:flex; gap:14px; padding-top:30px;
  }
  .gauge { flex:1; background:rgba(255,255,255,.05); border:1px solid rgba(255,255,255,.08);
    border-radius:14px; padding:14px 16px; }
  .gauge .lab { font-size:12px; color:#9a97c4; display:flex; justify-content:space-between; }
  .gauge .val { font-size:22px; font-weight:600; margin:6px 0 9px; }
  .bar { height:7px; border-radius:4px; background:rgba(255,255,255,.10); overflow:hidden; }
  .bar > i { display:block; height:100%; width:0%; border-radius:4px;
    background:linear-gradient(90deg,var(--accent),var(--accent2)); transition:width .5s; }
  .sub { font-size:11px; color:#7e7ba6; margin-top:6px; }
</style>
{{Theme.PageCss(theme)}}
</head>
<body>
  <div class="clock" id="clock">--:--</div>
  <div class="greeting" id="greet">Welcome to Crystal</div>

  <div class="logo">Crystal<span>Browser</span></div>

  <form action="{{Config.SearchFormAction(engine)}}" method="get">
    <input name="q" autofocus autocomplete="off" placeholder="Search {{Config.SearchName(engine)}} or type a URL…">
    <button class="go" type="submit">&#10148;</button>
  </form>

  <div class="tiles">
    <a class="tile" href="https://en.wikipedia.org">
      <img src="https://icons.duckduckgo.com/ip3/en.wikipedia.org.ico" alt="">Wikipedia</a>
    <a class="tile" href="https://news.ycombinator.com">
      <img src="https://icons.duckduckgo.com/ip3/news.ycombinator.com.ico" alt="">Hacker News</a>
    <a class="tile" href="https://github.com">
      <img src="https://icons.duckduckgo.com/ip3/github.com.ico" alt="">GitHub</a>
    <a class="tile" href="https://www.google.com">
      <svg class="ico" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M5 3h14l3 6-10 12L2 9l3-6z" stroke="#b39bff" stroke-width="1.6"
              stroke-linejoin="round" fill="rgba(124,108,255,.25)"/></svg>Google</a>
  </div>

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

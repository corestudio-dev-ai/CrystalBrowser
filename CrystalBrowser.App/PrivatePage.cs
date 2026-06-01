namespace CrystalBrowser.App;

/// <summary>Pages shown inside an Incognito window.</summary>
public static class PrivatePage
{
    /// <summary>
    /// Home / new-tab page for an Incognito window. Standard incognito privacy: an isolated,
    /// throwaway session that's wiped on close. Tor is still shipped in the install but is no
    /// longer wired into browsing, so we no longer claim network-level anonymity here.
    /// </summary>
    public static string PrivateHomeHtml() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Incognito — Crystal</title>
<style>
  :root { color-scheme:dark; --accent:#b39bff; }
  * { box-sizing:border-box; margin:0; padding:0; }
  html,body { height:100%; }
  body { font-family:'Segoe UI',system-ui,sans-serif; color:#e9e9ff;
    background:radial-gradient(1000px 600px at 50% -10%, #232136 0%, #15132a 55%, #0e0d1c 100%);
    min-height:100%; display:flex; flex-direction:column; align-items:center;
    padding:0 20px; overflow-x:hidden; }
  .shield { margin-top:11vh; }
  .shield svg { width:58px; height:58px; }
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
  <div class="shield">
    <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M4 12 L6 7 H18 L20 12" stroke="#b39bff" stroke-width="1.5" stroke-linejoin="round"/>
      <circle cx="7.5" cy="15" r="3" stroke="#b39bff" stroke-width="1.5"/>
      <circle cx="16.5" cy="15" r="3" stroke="#b39bff" stroke-width="1.5"/>
      <path d="M3 12 H21 M10.5 15 H13.5" stroke="#b39bff" stroke-width="1.5" stroke-linecap="round"/>
    </svg>
  </div>
  <h1>You've gone <span>Incognito</span></h1>
  <p class="lead">This is a private, throwaway session. Pages you visit here aren't saved to
     your history, and cookies, site data and the cache are wiped when you close the window.</p>

  <form action="https://www.google.com/search" method="get">
    <input name="q" autofocus autocomplete="off" placeholder="Search the web…">
    <button class="go" type="submit">&#10148;</button>
  </form>

  <div class="badges">
    <div class="badge"><div class="t">Nothing kept</div>
      <div class="d">History, cookies and cache from this window are erased on close.</div></div>
    <div class="badge"><div class="t">Isolated session</div>
      <div class="d">This window uses a separate, temporary profile of its own.</div></div>
    <div class="badge"><div class="t">Tor bundled</div>
      <div class="d">Tor still ships with the app, but it's no longer part of the browser.</div></div>
  </div>

  <p class="note">Incognito doesn't make you anonymous to the sites you visit, your network,
     or your employer — it just keeps this session off your own device.</p>
</body>
</html>
""";
}

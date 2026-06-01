# Crystal Browser

A fast, dark, Windows desktop web browser written in C# / .NET 8 (WPF), rendering the real
web with **WebView2** (embedded Chromium).

## Project

| Project | What it is |
|---------|-----------|
| `CrystalBrowser.App` | WPF tabbed browser UI (WebView2 / Chromium engine) |

## Features

- **Real Chromium rendering** via WebView2 — modern sites work like any browser.
- **Tabs** with titles and close buttons, plus a custom integrated title bar
  (minimize / maximize / close).
- **Smart address bar** — type a URL to navigate, or anything else to search **Google**.
- **Offline home / new-tab page** with a live clock, quick links, and a system widget.
- **Forced dark mode on every page** — Chromium's auto-dark engine
  (`--enable-features=WebContentsForceDark`) plus a dark `prefers-color-scheme`, so even
  sites without a dark theme render dark.
- **Private windows with bundled Tor** — Tor ships inside Crystal Browser (no separate
  install), so every page in a private window is routed through the **Tor network** for
  complete privacy: your real IP is never exposed and the throwaway profile is wiped on
  close. A live percentage bar shows load progress while pages travel the slower Tor route.
- **Default-browser prompt** — a Chrome-style banner offers to set Crystal as your default
  browser, deep-linking to the Windows *Default apps* settings.
- **Edit mode** — toggle `document.designMode` to edit any live page in place.
- **Live CPU / RAM monitor** in the status bar and on the home page.
- **Settings / About** page (⚙ in the toolbar).

## Run it

```powershell
dotnet run --project CrystalBrowser.App
```

## Build a release / installer

Publish a self-contained build (bundles the .NET runtime):

```powershell
dotnet publish CrystalBrowser.App -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -o build/CrystalBrowser
```

Then compile the installer with Inno Setup:

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\CrystalBrowser.iss
```

This produces `installer\CrystalBrowserSetup.exe` — a full wizard installer that bundles
.NET, auto-installs the WebView2 runtime if missing, lets you choose the location, and
creates Start Menu / desktop shortcuts.

> The only runtime requirement is the **Microsoft Edge WebView2 Runtime** (pre-installed on
> virtually all Windows 11 machines; the installer fetches it automatically if absent).

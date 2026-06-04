# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Crystal Browser is a Windows desktop web browser: a single WPF app (`CrystalBrowser.App`, .NET 8, `net8.0-windows`) that embeds **WebView2** (Chromium) to render the real web. There is no backend or server — the app is fully self-contained and searches the live web via Google.

## Commands

```powershell
# Run
dotnet run --project CrystalBrowser.App

# Build
dotnet build CrystalBrowser.App

# Publish a self-contained, single-file release (bundles the .NET runtime)
dotnet publish CrystalBrowser.App -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -o build/CrystalBrowser

# Build the installer (after publishing to build/CrystalBrowser)
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\CrystalBrowser.iss
```

There are no tests, linter, or CI configured. The only runtime requirement is the Microsoft Edge WebView2 Runtime (the installer fetches it if absent).

## Release workflow

When asked to cut a release, do these steps **in order** (the descriptions matter — releases must always ship with proper, human-readable notes):

1. **Implement** exactly what the user asked for, and build (`dotnet build`) to confirm it compiles cleanly.
2. **Bump the version** in `Config.cs` (`Config.Version`) and `installer\CrystalBrowser.iss` (`AppVersion`) — keep the two in sync.
3. **Commit** the change on `main` with a clear message, then `git push origin main`. Commit *before* creating the release so the tag points at the right commit.
4. **Publish** the self-contained build and **compile the installer** (the two commands above) to produce `installer\CrystalBrowserSetup.exe`.
5. **Create the GitHub release** with `gh release create vX.Y.Z installer\CrystalBrowserSetup.exe --target main --title "Crystal Browser X.Y.Z" --notes "…"`. Tag is `vX.Y.Z` (matches `Config.Version`); the asset **must** be `CrystalBrowserSetup.exe` (the updater grabs the first `.exe` asset).
6. **Always write real release notes** — a short summary line plus bullets of what changed in this version, in plain language for end users. Never publish an empty or one-line release. Edit past releases with `gh release edit vX.Y.Z --notes "…"` if they're missing good notes.

The in-app updater compares the release tag to `Config.Version`, so a release with a higher tag auto-updates everyone on launch.

## Architecture

The whole UI lives in **`MainWindow.xaml` / `MainWindow.xaml.cs`** (~850 lines) — tab strip, custom title bar, address bar, status bar, home page hosting, and per-tab `WebView2` management. `BrowserTab` is the per-tab model. New windows are spawned by constructing `MainWindow`; `App.xaml.cs` opens the first window and, when Windows launches Crystal as the default browser, passes the URL via `new MainWindow(url)`.

Key cross-cutting concepts:

- **Profiles are the storage unit.** Each Crystal profile gets an isolated WebView2 user-data folder under `%LocalAppData%\CrystalBrowser\Profiles\<name>` (`ProfileStore.DataDir`). History, cookies, logins, and bookmarks all live inside the active profile (`BookmarkStore` is constructed with the active profile name). `ProfileStore.EnsureActive()` runs on window construction to guarantee a valid active profile (falls back to "Default"). The profile list persists in `%AppData%\CrystalBrowser\profiles.json`.
- **Incognito** windows (`new MainWindow(tor: true)`) use an ephemeral data folder under `%Temp%\CrystalBrowserPrivate\<guid>` that is deleted on close. (Despite the `tor:` parameter name and a bundled Tor expert bundle, Tor is no longer wired into browsing.)
- **Forced dark mode**: the shared `CoreWebView2Environment` is created with `AdditionalBrowserArguments = "--enable-features=WebContentsForceDark"` unless the UI theme is light. Created lazily and shared across tabs (`_env` / `_envTask`).
- **uBlock Origin Lite (MV3)** is loaded as an extension on startup. The extension folder is found by walking up from the running binary looking for `ublock\manifest.json` (`third_party\ublock` is bundled into the output via the `.csproj` `Content` items).
- **Settings** (`AppSettings` / `SettingsStore`) persist as `%AppData%\CrystalBrowser\settings.json`: tab layout (horizontal/vertical), startup behavior (newtab/url), accent color, theme (dark/light), active profile. `Theme.cs` applies theme; `SettingsPage.cs` / `PrivatePage.cs` / `HomePage.cs` build their respective pages as injected HTML/markup rather than separate XAML windows.
- **Updater** (`Updater.cs`) polls `Config.UpdateRepo`'s GitHub Releases (`/releases/latest`) every 60s until it gets a definitive answer, then downloads and runs the installer. Skipped in incognito.

## Conventions

- `Config.cs` holds app-wide constants. **`Config.Version` must be bumped each release and the GitHub release tagged to match**; also keep `installer\CrystalBrowser.iss` `AppVersion` in sync.
- Persistent state is split deliberately: per-profile data under `%LocalAppData%\CrystalBrowser\Profiles\<name>`, app-level config under `%AppData%\CrystalBrowser`.
- Persistence code (settings, profiles, bookmarks) follows a best-effort pattern — load/save wrapped in `try/catch` that silently falls back to defaults; preserve this rather than throwing.
- `third_party/` (Tor, uBlock) is bundled into build output via `.csproj` `<Content>` links — don't reference it by source path at runtime; locate it relative to the running binary.

Ignore the `build/` and `CrystalBrowser.App/bin/` directories — they contain published output and the WebView2 EBWebView cache, not source.

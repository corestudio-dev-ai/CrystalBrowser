using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CrystalBrowser.App;

/// <summary>
/// Crystal Browser main window. Custom tab strip, a smart address bar that routes plain
/// queries to Google, an offline home page, an in-place page editor,
/// and a live CPU/RAM monitor in the status bar.
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _active;

    // Shared WebView2 environment configured to force dark mode on every page.
    private CoreWebView2Environment? _env;
    private Task<CoreWebView2Environment>? _envTask;

    // Incognito mode: an isolated, throwaway WebView2 profile wiped on close.
    private readonly bool _incognito;
    private readonly string? _privateDataDir; // ephemeral profile folder for incognito windows
    // Normal windows browse inside the active Crystal profile's isolated data folder.
    private readonly string? _profileDataDir;

    private readonly BookmarkStore _bookmarks;
    // Crystal autofill: saved logins for the active profile (null in incognito — never saved there).
    private readonly AutofillStore? _autofill;
    private readonly SystemMonitor _monitor = new();
    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    // Private-window loading bar: a simulated percentage that climbs while a page loads over
    // Tor (which gives no real progress events), so the user can see it's actually working.
    private readonly DispatcherTimer _loadTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private double _loadPct;

    // Pings GitHub Releases every 60s and keeps pinging while checks fail (offline/not ready);
    // stops entirely once it gets a definitive answer — either up to date or an update found.
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromSeconds(60) };

    // A URL to open on launch instead of the home page (set when Windows starts us as the
    // default browser and hands us a link to open).
    private readonly string? _initialUrl;

    // True while the first-run onboarding page is showing (changes how theme messages behave).
    private bool _onboarding;

    public MainWindow() : this(tor: false) { }

    public MainWindow(string url) : this(tor: false) { _initialUrl = url; }

    public MainWindow(bool tor)
    {
        InitializeComponent();
        _incognito = tor;
        ProfileStore.EnsureActive(); // profiles are the storage unit — guarantee a valid one
        SettingsStore.Current.TabLayout = "vertical"; // 1.8.1: tabs always live in the vertical rail
        // Bookmarks and history both live in the active profile (cemented in 1.5.3).
        _bookmarks = new BookmarkStore(SettingsStore.Current.ActiveProfile);
        if (!_incognito) _autofill = new AutofillStore(SettingsStore.Current.ActiveProfile);
        ApplyTheme(SettingsStore.Current.Theme);
        ApplyAccent(SettingsStore.Current.Accent);
        ApplyFrameColor(SettingsStore.Current.FrameColor);
        if (_incognito)
        {
            _privateDataDir = Path.Combine(Path.GetTempPath(), "CrystalBrowserPrivate", Guid.NewGuid().ToString("N"));
            Title = "Crystal Browser — Incognito";
            PrivateBadge.Visibility = Visibility.Visible;
            BtnPrivate.Visibility = Visibility.Collapsed; // no nested incognito windows
        }
        else
        {
            var profile = SettingsStore.Current.ActiveProfile;
            _profileDataDir = ProfileStore.DataDir(profile);
            if (!string.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase))
                Title = $"Crystal Browser — {profile}";
        }
        _statsTimer.Tick += (_, _) => UpdateSystemStats();
        _statsTimer.Start();
        _loadTimer.Tick += (_, _) => TickLoadProgress();
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        Closed += (_, _) => { _loadTimer.Stop(); _updateTimer.Stop(); _monitor.Dispose(); CleanupPrivateProfile(); };
        StateChanged += OnStateChanged;
        Loaded += (_, _) =>
        {
            FitToScreen();
            ApplyTabLayout(SettingsStore.Current.TabLayout);
            RenderBookmarks();
            var startupUrl = StartupTarget();
            // First launch (a normal window, no link handed to us): run the onboarding flow.
            bool firstRun = !_incognito && !SettingsStore.Current.OnboardingDone
                            && string.IsNullOrWhiteSpace(_initialUrl);
            // Returning user who just updated: show the in-app changelog ("What's new").
            bool whatsNew = !_incognito && !firstRun && SettingsStore.Current.OnboardingDone
                            && SettingsStore.Current.LastSeenVersion != Config.Version
                            && string.IsNullOrWhiteSpace(_initialUrl);
            if (firstRun)
            {
                _onboarding = true;
                AddNewTab(onboarding: true);
            }
            else if (whatsNew)
            {
                AddNewTab(whatsNew: true);
                SettingsStore.Current.LastSeenVersion = Config.Version; // seen — don't show again
                SettingsStore.Save();
            }
            else if (!string.IsNullOrWhiteSpace(_initialUrl))
                AddNewTab(url: _initialUrl);
            else if (startupUrl != null)
                AddNewTab(url: startupUrl);
            else
                AddNewTab(home: true);
            if (!_incognito)
            {
                _ = CheckForUpdatesAsync(); // first check immediately…
                _updateTimer.Start();       // …then keep polling every 60s until one is found
                ShowUnsupportedNagIfNeeded(); // remind end-of-life builds to update (non-blocking)
                if (!_onboarding) ShowDefaultBrowserNag(); // onboarding has its own "set default" step
            }
        };
    }

    // The page to open at launch per the "On startup" setting (normal windows only), or null.
    private string? StartupTarget()
    {
        if (_incognito) return null;
        var s = SettingsStore.Current;
        if (s.Startup == "url" && !string.IsNullOrWhiteSpace(s.StartupUrl))
            return s.StartupUrl;
        return null;
    }

    private void CleanupPrivateProfile()
    {
        if (_privateDataDir != null && Directory.Exists(_privateDataDir))
            try { Directory.Delete(_privateDataDir, recursive: true); } catch { }
    }

    private void BtnPrivate_Click(object sender, RoutedEventArgs e)
    {
        // Incognito is now a standard isolated, throwaway session (no Tor routing).
        new MainWindow(tor: true).Show();
    }

    // ----- Win11 Mica transparency ----------------------------------------

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38; // Win11 22621+
    private const int BackdropMica = 2;

    // Apply the Mica backdrop (and dark title frame) once the HWND exists. No-op on older
    // Windows — the call simply fails and we fall back to the solid chrome backgrounds.
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int dark = IsLight ? 0 : 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
            int backdrop = BackdropMica;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }
        catch { /* pre-Win11 — keep the solid look */ }
    }

    // Ensure the window never launches larger than the available screen area,
    // so the title-bar buttons on the right are always on-screen.
    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea; // excludes the taskbar
        if (Width > area.Width) Width = area.Width * 0.92;
        if (Height > area.Height) Height = area.Height * 0.92;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
    }

    // ----- Window caption controls ----------------------------------------

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMax_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // WindowChrome maximizes edge-to-edge and clips; inset to the work area, and
        // swap the maximize glyph for a restore glyph.
        bool max = WindowState == WindowState.Maximized;
        // Inset the whole neon frame when maximized so its border stays on-screen.
        FrameBorder.Margin = max ? new Thickness(7) : new Thickness(0);
        BtnMax.Content = max ? "" : ""; // restore : maximize (Segoe MDL2)
        BtnMax.ToolTip = max ? "Restore" : "Maximize";
    }

    // ----- Tab model ------------------------------------------------------

    private sealed class BrowserTab
    {
        public required WebView2 View;
        public required Border Header;
        public required TextBlock Title;
        public bool IsHome;
        public TabGroup? Group;
    }

    /// <summary>An in-window tab group: a named, coloured, collapsible cluster in the vertical rail.</summary>
    private sealed class TabGroup
    {
        public required string Name;
        public required Color Color;
        public bool Collapsed;
    }

    private readonly List<TabGroup> _groups = new();

    // Colours cycled through as new groups are created.
    private static readonly Color[] GroupColors =
    {
        Color.FromRgb(0xFF, 0x7A, 0x1A), Color.FromRgb(0x4F, 0x6B, 0xFF), Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0xFF, 0x3D, 0x7E), Color.FromRgb(0x00, 0xB4, 0xD8), Color.FromRgb(0xB3, 0x9B, 0xFF),
    };

    private WebView2? Current => _active?.View;

    /// <summary>
    /// Lazily create one shared WebView2 environment with Chromium's force-dark feature
    /// enabled, so every page — even sites with no dark theme — renders in dark mode.
    /// </summary>
    private Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        if (_env != null) return Task.FromResult(_env);
        return _envTask ??= CreateEnvironmentAsync();
    }

    private async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        // Force-dark every page only in the dark theme; let pages render normally in light.
        var args = IsLight ? "" : "--enable-features=WebContentsForceDark";
        var options = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = args };
        options.AreBrowserExtensionsEnabled = true; // needed to load the bundled uBlock Origin Lite
        // Incognito windows use an isolated, ephemeral folder; normal windows use the active
        // Crystal profile's folder so each profile's cookies/logins stay separate.
        var dataDir = _incognito ? _privateDataDir : _profileDataDir;
        _env = await CoreWebView2Environment.CreateAsync(null, dataDir, options);
        return _env;
    }

    // ----- Settings page bridge -------------------------------------------

    private async void HandleSettingsMessage(string? type, JsonElement root, CoreWebView2 core)
    {
        var s = SettingsStore.Current;
        switch (type)
        {
            case "getSettings":
                await PushSettings(core);
                break;
            case "getLogins":
                await PushLogins(core);
                break;
            case "removeLogin":
                _autofill?.Remove(root.GetProperty("origin").GetString() ?? "",
                    root.TryGetProperty("username", out var ru) ? ru.GetString() ?? "" : "");
                await PushLogins(core);
                break;
            case "setTabLayout":
                s.TabLayout = root.GetProperty("value").GetString() ?? "horizontal";
                SettingsStore.Save();
                ApplyTabLayout(s.TabLayout); // live
                break;
            case "setStartup":
                s.Startup = root.GetProperty("mode").GetString() ?? "newtab";
                if (root.TryGetProperty("url", out var u)) s.StartupUrl = u.GetString() ?? "";
                SettingsStore.Save();
                break;
            case "setAccent":
                s.Accent = root.GetProperty("value").GetString() ?? "#4F6BFF";
                SettingsStore.Save();
                ApplyAccent(s.Accent); // live
                break;
            case "setFrameColor":
                s.FrameColor = root.GetProperty("value").GetString() ?? "#4F6BFF";
                SettingsStore.Save();
                ApplyFrameColor(s.FrameColor); // live
                break;
            case "openChangelog":
                if (_active != null)
                {
                    _active.IsHome = false;
                    _active.Title.Text = "Changelog";
                    core.NavigateToString(ChangelogPage.Html(s.Theme));
                }
                break;
            case "resetSettings":
                ResetSettingsAndRestart(core);
                break;
            case "setTheme":
                s.Theme = root.GetProperty("value").GetString() ?? "dark";
                s.Accent = Theme.Get(s.Theme).Accent; // each theme carries its own accent
                SettingsStore.Save();
                ApplyTheme(s.Theme);  // chrome updates live
                ApplyAccent(s.Accent);
                if (_onboarding)
                {
                    // Hot-swap the onboarding page's theme CSS so it previews without a reload.
                    var css = JsonSerializer.Serialize(Theme.PageCssInner(s.Theme));
                    await core.ExecuteScriptAsync($"window.crystalTheme && window.crystalTheme({css})");
                }
                else
                {
                    core.NavigateToString(SettingsPage.Html(s.Theme)); // re-render this page themed
                }
                break;
            case "setSearchEngine":
                s.SearchEngine = root.GetProperty("value").GetString() ?? "google";
                SettingsStore.Save();
                if (!_onboarding) await PushSettings(core);
                break;
            case "addProfile":
                ProfileStore.Add(root.GetProperty("name").GetString() ?? "");
                await PushSettings(core);
                break;
            case "switchProfile":
                s.ActiveProfile = root.GetProperty("name").GetString() ?? "Default";
                SettingsStore.Save();
                // Reopen in the chosen profile (its own isolated WebView2 data folder).
                new MainWindow().Show();
                Close();
                break;
            case "signIn":
                ProfileStore.SetAccountEmail(s.ActiveProfile, root.GetProperty("email").GetString());
                await PushSettings(core);
                break;
            case "signOut":
                ProfileStore.SetAccountEmail(s.ActiveProfile, null);
                await PushSettings(core);
                break;

            // ----- Onboarding (first run) -----
            case "getOnboarding":
                await PushOnboarding(core);
                break;
            case "importBookmarks":
                ImportBookmarks(root.GetProperty("source").GetString(), core);
                break;
            case "setDefaultBrowser":
                DefaultBrowser.OpenDefaultAppsSettings();
                break;
            case "finishOnboarding":
                s.OnboardingDone = true;
                s.LastSeenVersion = Config.Version; // new users start current — no "what's new" next launch
                SettingsStore.Save();
                _onboarding = false;
                if (_active != null) GoHome(_active);
                if (!_incognito) ShowDefaultBrowserNag();
                break;
        }
    }

    // Push the onboarding page's initial state: themes, current selections, and importable browsers.
    private static async Task PushOnboarding(CoreWebView2 core)
    {
        var s = SettingsStore.Current;
        var themes = Theme.All.Select(t => new { key = t.Key, name = t.Name, swatch = t.Accent });
        var sources = BookmarkImport.Available()
            .Select(src => new { id = src.Id, name = src.Name, count = BookmarkImport.Read(src.Path).Count });
        var payload = JsonSerializer.Serialize(new
        {
            theme = s.Theme,
            engine = s.SearchEngine,
            themes,
            sources,
        });
        await core.ExecuteScriptAsync($"window.crystalOnboard && window.crystalOnboard({payload})");
    }

    // Import bookmarks from the chosen installed browser into the active profile, then report the count.
    private async void ImportBookmarks(string? sourceId, CoreWebView2 core)
    {
        int added = 0;
        var src = BookmarkImport.Available().FirstOrDefault(x => x.Id == sourceId);
        if (src != null) added = _bookmarks.Import(BookmarkImport.Read(src.Path));
        RenderBookmarks();
        await core.ExecuteScriptAsync(
            $"window.crystalImportResult && window.crystalImportResult({JsonSerializer.Serialize(sourceId)},{added})");
    }

    // Reset all settings to defaults, show a brief animation, then relaunch the app (Opera-style).
    private async void ResetSettingsAndRestart(CoreWebView2 core)
    {
        core.NavigateToString(ResetPage.Html(SettingsStore.Current.Theme));
        SettingsStore.Reset();
        await Task.Delay(1700);
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                });
        }
        catch { /* if relaunch fails the user can reopen manually */ }
        Application.Current.Shutdown();
    }

    private static async Task PushSettings(CoreWebView2 core)
    {
        var s = SettingsStore.Current;
        var payload = JsonSerializer.Serialize(new
        {
            tabLayout = s.TabLayout,
            startup = s.Startup,
            startupUrl = s.StartupUrl,
            accent = s.Accent,
            frameColor = s.FrameColor,
            theme = s.Theme,
            searchEngine = s.SearchEngine,
            activeProfile = s.ActiveProfile,
            profiles = ProfileStore.Items,
            account = ProfileStore.AccountEmail(s.ActiveProfile),
        });
        await core.ExecuteScriptAsync($"window.crystalSettings && window.crystalSettings({payload})");
    }

    // Push the saved-login list (origin + username only — never the password) to the Settings page.
    private async Task PushLogins(CoreWebView2 core)
    {
        var list = _autofill?.Items.Select(l => new { origin = l.Origin, username = l.Username })
                   ?? Enumerable.Empty<object>();
        await core.ExecuteScriptAsync(
            $"window.crystalLogins && window.crystalLogins({JsonSerializer.Serialize(list)})");
    }

    // ----- Bundled uBlock Origin Lite -------------------------------------

    private bool _ublockLoaded;

    /// <summary>Load the bundled uBlock Origin Lite (MV3) extension once per profile (best effort).</summary>
    private async Task EnsureUBlockAsync(CoreWebView2 core)
    {
        if (_ublockLoaded) return;
        _ublockLoaded = true;
        try
        {
            var dir = LocateUBlock();
            if (dir != null)
                await core.Profile.AddBrowserExtensionAsync(dir);
        }
        catch
        {
            // Extension API unavailable (older WebView2) or load failed — browse without it.
            _ublockLoaded = false;
        }
    }

    // Find the shipped uBlock folder by walking up from the running binary: <root>\ublock\manifest.json
    private static string? LocateUBlock()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "ublock");
            if (File.Exists(Path.Combine(candidate, "manifest.json"))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private void AddNewTab(bool home = false, string? url = null, bool onboarding = false, bool whatsNew = false)
    {
        var web = new WebView2 { Visibility = Visibility.Collapsed };
        BrowserHost.Children.Add(web);

        var title = new TextBlock
        {
            Text = "New Tab", Foreground = (Brush)Resources["TabTextInactive"], FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var close = new Button
        {
            Content = "✕", Style = (Style)FindResource("TabClose"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0)
        };
        // title fills, close button pinned to the right (works for both layouts).
        var panel = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(close, Dock.Right);
        panel.Children.Add(close);
        panel.Children.Add(title);
        var header = new Border { Cursor = Cursors.Hand, Child = panel };

        // Header can live inside the WindowChrome caption strip (horizontal) — make it clickable.
        WindowChrome.SetIsHitTestVisibleInChrome(header, true);

        var tab = new BrowserTab { View = web, Header = header, Title = title };
        _tabs.Add(tab);
        StyleTabHeader(tab, SettingsStore.Current.TabLayout);
        PlaceTab(tab);

        header.MouseLeftButtonUp += (_, _) => Activate(tab);
        close.Click += (s, e) => { e.Handled = true; CloseTab(tab); };

        // Right-click a tab to group it (vertical rail shows the coloured group headers).
        var menu = new ContextMenu();
        menu.Opened += (_, _) => BuildTabMenu(menu, tab);
        header.ContextMenu = menu;

        Activate(tab);
        InitWebView(tab, home, url, onboarding, whatsNew);
    }

    private void CloseTab(BrowserTab tab)
    {
        int idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        (tab.Header.Parent as Panel)?.Children.Remove(tab.Header);
        BrowserHost.Children.Remove(tab.View);
        tab.View.Dispose();
        PruneEmptyGroups();
        if (SettingsStore.Current.TabLayout == "vertical") RebuildVerticalStrip();

        if (_tabs.Count == 0) { Close(); return; }
        if (_active == tab)
            Activate(_tabs[Math.Min(idx, _tabs.Count - 1)]);
    }

    private void Activate(BrowserTab tab)
    {
        _active = tab;
        foreach (var t in _tabs)
        {
            bool on = t == tab;
            t.View.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            t.Header.Background = on ? (Brush)Resources["TabActiveBg"] : Brushes.Transparent;
            t.Title.Foreground = on
                ? (Brush)Resources["TabTextActive"] : (Brush)Resources["TabTextInactive"];
        }
        SyncChrome();
    }

    // ----- Tab layout (horizontal default / vertical option) --------------

    private Panel ActiveTabStrip =>
        SettingsStore.Current.TabLayout == "vertical" ? VerticalTabStrip : HorizontalTabStrip;

    // Style a tab header for the chosen layout: a compact top pill, or a full-width rail row.
    private static void StyleTabHeader(BrowserTab tab, string layout)
    {
        var panel = (DockPanel)tab.Header.Child;
        if (layout == "vertical")
        {
            tab.Header.CornerRadius = new CornerRadius(10);
            tab.Header.HorizontalAlignment = HorizontalAlignment.Stretch;
            tab.Header.Width = double.NaN;
            tab.Header.Margin = new Thickness(0, 2, 0, 2);
            panel.Margin = new Thickness(12, 8, 8, 8);
            tab.Title.MaxWidth = double.PositiveInfinity;
        }
        else
        {
            tab.Header.CornerRadius = new CornerRadius(10, 10, 0, 0);
            tab.Header.HorizontalAlignment = HorizontalAlignment.Left;
            tab.Header.Width = double.NaN;
            tab.Header.Margin = new Thickness(2, 4, 0, 0);
            panel.Margin = new Thickness(12, 7, 6, 7);
            tab.Title.MaxWidth = 150;
        }
    }

    // Apply a layout: show the matching strip, hide the other, and move every header across.
    private void ApplyTabLayout(string layout)
    {
        bool vertical = layout == "vertical";
        TabSidebar.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        HorizontalTabBar.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;

        foreach (var t in _tabs)
        {
            (t.Header.Parent as Panel)?.Children.Remove(t.Header);
            StyleTabHeader(t, layout);
        }
        if (vertical) RebuildVerticalStrip();
        else foreach (var t in _tabs) HorizontalTabStrip.Children.Add(t.Header);
        if (_active != null) Activate(_active); // restore selected-tab highlight
    }

    // Add a freshly created tab's header to the active strip (rebuilds the grouped vertical rail).
    private void PlaceTab(BrowserTab tab)
    {
        if (SettingsStore.Current.TabLayout == "vertical") RebuildVerticalStrip();
        else HorizontalTabStrip.Children.Add(tab.Header);
    }

    // Rebuild the vertical rail: ungrouped tabs first, then each group's coloured header
    // followed by its (non-collapsed) tabs. Tab order within a section follows creation order.
    private void RebuildVerticalStrip()
    {
        VerticalTabStrip.Children.Clear();
        foreach (var t in _tabs)
            (t.Header.Parent as Panel)?.Children.Remove(t.Header);

        foreach (var t in _tabs.Where(t => t.Group == null))
            VerticalTabStrip.Children.Add(t.Header);

        foreach (var g in _groups)
        {
            VerticalTabStrip.Children.Add(BuildGroupHeader(g));
            if (g.Collapsed) continue;
            foreach (var t in _tabs.Where(t => t.Group == g))
                VerticalTabStrip.Children.Add(t.Header);
        }
    }

    // A clickable group header row: a colour dot, the name, and a collapse chevron.
    private Border BuildGroupHeader(TabGroup g)
    {
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 10, Height = 10, Fill = new SolidColorBrush(g.Color),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
        };
        var name = new TextBlock
        {
            Text = g.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Resources["TextPrimary"], VerticalAlignment = VerticalAlignment.Center
        };
        var chevron = new TextBlock
        {
            Text = g.Collapsed ? "▸" : "▾", FontSize = 11, Foreground = (Brush)Resources["TextMuted"],
            VerticalAlignment = VerticalAlignment.Center
        };
        var panel = new DockPanel { LastChildFill = false, Margin = new Thickness(12, 8, 10, 4) };
        DockPanel.SetDock(chevron, Dock.Right);
        panel.Children.Add(chevron);
        panel.Children.Add(dot);
        panel.Children.Add(name);
        var border = new Border
        {
            Child = panel, Cursor = Cursors.Hand, CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x22, g.Color.R, g.Color.G, g.Color.B)),
            Margin = new Thickness(0, 4, 0, 2)
        };
        border.MouseLeftButtonUp += (_, _) => { g.Collapsed = !g.Collapsed; RebuildVerticalStrip(); if (_active != null) Activate(_active); };
        return border;
    }

    // Populate a tab's right-click menu with the current grouping options.
    private void BuildTabMenu(ContextMenu menu, BrowserTab tab)
    {
        menu.Items.Clear();
        var add = new MenuItem { Header = "Add to new group…" };
        add.Click += (_, _) => AddTabToNewGroup(tab);
        menu.Items.Add(add);

        foreach (var g in _groups.Where(g => g != tab.Group))
        {
            var item = new MenuItem { Header = $"Move to \"{g.Name}\"" };
            item.Click += (_, _) => { tab.Group = g; RebuildVerticalStrip(); if (_active != null) Activate(_active); };
            menu.Items.Add(item);
        }
        if (tab.Group != null)
        {
            var remove = new MenuItem { Header = "Remove from group" };
            remove.Click += (_, _) => { tab.Group = null; PruneEmptyGroups(); RebuildVerticalStrip(); if (_active != null) Activate(_active); };
            menu.Items.Add(remove);
        }
    }

    private void AddTabToNewGroup(BrowserTab tab)
    {
        var g = new TabGroup { Name = $"Group {_groups.Count + 1}", Color = GroupColors[_groups.Count % GroupColors.Length] };
        _groups.Add(g);
        tab.Group = g;
        if (SettingsStore.Current.TabLayout != "vertical")
            SetStatus("Tab grouped — switch to vertical tabs (Settings) to see groups.");
        RebuildVerticalStrip();
        if (_active != null) Activate(_active);
    }

    private void PruneEmptyGroups() => _groups.RemoveAll(g => !_tabs.Any(t => t.Group == g));

    // ----- Appearance (accent colour) -------------------------------------

    private void ApplyAccent(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            Resources["AccentBrush"] = new SolidColorBrush(color);
        }
        catch { /* invalid hex — keep the existing accent */ }
    }

    // The user-chosen window frame border colour.
    private void ApplyFrameColor(string hex)
    {
        try { Resources["FrameBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { /* invalid hex — keep the existing frame colour */ }
    }

    // Swap the theme surface brushes from the chosen theme's palette. Live for the WPF chrome;
    // web/offline pages pick up the theme when they're next rendered (they're styled separately).
    private void ApplyTheme(string theme)
    {
        var t = Theme.Get(theme);
        void Set(string key, string hex) =>
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

        Set("WindowBg",        t.WindowBg);
        Set("ChromeBg",        t.ChromeBg);
        Set("ChromeBg2",       t.ChromeBg2);
        Set("SurfaceBg",       t.SurfaceBg);
        Set("TextPrimary",     t.TextPrimary);
        Set("TextMuted",       t.TextMuted);
        Set("TabTextActive",   t.TabTextActive);
        Set("TabTextInactive", t.TabTextInactive);
        Set("TabActiveBg",     t.TabActiveBg);

        // The animated water layer only makes sense behind Oceanic's translucent glass chrome;
        // other themes use opaque chrome (or the Mica backdrop), so hide it there.
        WaterBg.Visibility = string.Equals(theme, "oceanic", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool IsLight => Theme.IsLight(SettingsStore.Current.Theme);

    private async void InitWebView(BrowserTab tab, bool home, string? url, bool onboarding = false, bool whatsNew = false)
    {
        var web = tab.View;
        // Themed canvas behind every page so navigations don't flash a contrasting colour.
        var wc = (Color)ColorConverter.ConvertFromString(Theme.Get(SettingsStore.Current.Theme).WindowBg);
        web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, wc.R, wc.G, wc.B);
        await web.EnsureCoreWebView2Async(await GetEnvironmentAsync());
        var core = web.CoreWebView2;

        await EnsureUBlockAsync(core); // load the bundled ad blocker once per profile

        // Crystal autofill: inject the form-watcher into every page (normal windows only).
        if (_autofill != null)
            try { await core.AddScriptToExecuteOnDocumentCreatedAsync(AutofillScript); } catch { }

        // Tell sites which scheme we prefer (Google etc. honour this natively).
        core.Profile.PreferredColorScheme = IsLight
            ? CoreWebView2PreferredColorScheme.Light
            : CoreWebView2PreferredColorScheme.Dark;

        core.DocumentTitleChanged += (_, _) =>
            tab.Title.Text = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "New Tab" : core.DocumentTitle;

        core.SourceChanged += (_, _) => { if (tab == _active) SyncChrome(); };
        core.NavigationStarting += (_, e) =>
        {
            tab.IsHome = false;
            if (tab == _active) { SetStatus($"Loading {e.Uri} …"); StartLoadProgress(); }
        };
        core.NavigationCompleted += (_, _) =>
        {
            if (tab == _active) { SyncChrome(); ReapplyEditMode(web); FinishLoadProgress(); }
            SetStatus("Done");
        };
        core.NewWindowRequested += (_, e) => { e.Handled = true; AddNewTab(url: e.Uri); };

        // Bridge for messages the offline pages send us (e.g. the Settings "Check for updates").
        core.WebMessageReceived += async (_, e) =>
        {
            string msg;
            try { msg = e.TryGetWebMessageAsString(); } catch { return; }
            if (msg == "check-updates") { await ManualCheckForUpdatesAsync(core); return; }
            // Everything else is a JSON message (settings page or Crystal autofill).
            try
            {
                using var doc = JsonDocument.Parse(msg);
                var type = doc.RootElement.GetProperty("type").GetString();
                if (type == "autofill-request") { await HandleAutofillRequest(doc.RootElement, core); return; }
                if (type == "autofill-submit") { HandleAutofillSubmit(doc.RootElement); return; }
                HandleSettingsMessage(type, doc.RootElement, core);
            }
            catch { /* not a settings message */ }
        };

        if (onboarding)
        {
            tab.IsHome = false;
            tab.Title.Text = "Welcome to Crystal";
            core.NavigateToString(Onboarding.Html(SettingsStore.Current.Theme));
        }
        else if (whatsNew)
        {
            tab.IsHome = false;
            tab.Title.Text = "What's new";
            core.NavigateToString(ChangelogPage.Html(SettingsStore.Current.Theme, Config.Version));
        }
        else if (home || url == null)
            GoHome(tab);
        else
            Navigate(web, url);
    }

    // ----- Home page ------------------------------------------------------

    private void GoHome(BrowserTab tab)
    {
        tab.IsHome = true;
        tab.Title.Text = "New Tab";
        var theme = SettingsStore.Current.Theme;
        var engine = SettingsStore.Current.SearchEngine;
        // Incognito windows get the incognito home; normal windows get the regular home page.
        var html = _incognito ? PrivatePage.PrivateHomeHtml(theme, engine) : HomePage.Html(theme, engine);
        tab.View.CoreWebView2?.NavigateToString(html);
        if (tab == _active) { AddressBar.Text = ""; AddressBar.Focus(); }
    }

    // ----- Navigation -----------------------------------------------------

    private void Navigate(WebView2 web, string input)
    {
        if (web.CoreWebView2 == null) return;
        var internalPage = ResolveInternal(input);
        if (internalPage != null) web.CoreWebView2.NavigateToString(internalPage);
        else web.CoreWebView2.Navigate(Resolve(input));
    }

    // Crystal's own internal pages, addressable as crystal://… (like edge://… or chrome://…).
    // Returns the offline HTML to show, or null if the input isn't a crystal:// address.
    private string? ResolveInternal(string input)
    {
        var s = input.Trim().ToLowerInvariant();
        if (!s.StartsWith("crystal://")) return null;
        var page = s["crystal://".Length..].TrimEnd('/');
        var theme = SettingsStore.Current.Theme;
        return page switch
        {
            "whatsnew" or "whats-new" => ChangelogPage.Html(theme, Config.Version),
            "changelog"               => ChangelogPage.Html(theme),
            "settings"                => SettingsPage.Html(theme),
            "home" or "newtab"        => HomePage.Html(theme, SettingsStore.Current.SearchEngine),
            _                          => ChangelogPage.Html(theme), // unknown crystal:// page
        };
    }

    private static string Resolve(string input)
    {
        var search = Config.SearchQueryUrl(SettingsStore.Current.SearchEngine);
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return search;
        if (input.StartsWith("http://") || input.StartsWith("https://")) return input;
        if (!input.Contains(' ') && input.Contains('.') && !input.Contains('?')) return "https://" + input;
        return search + Uri.EscapeDataString(input);
    }

    private void GoFromAddressBar()
    {
        if (Current == null) return;
        Navigate(Current, AddressBar.Text);
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) GoFromAddressBar(); }
    private void BtnGo_Click(object sender, RoutedEventArgs e) => GoFromAddressBar();
    private void BtnBack_Click(object sender, RoutedEventArgs e) { if (Current?.CanGoBack == true) Current.GoBack(); }
    private void BtnForward_Click(object sender, RoutedEventArgs e) { if (Current?.CanGoForward == true) Current.GoForward(); }
    private void BtnReload_Click(object sender, RoutedEventArgs e) => Current?.Reload();
    private void BtnHome_Click(object sender, RoutedEventArgs e) { if (_active != null) GoHome(_active); }
    private void BtnNewTab_Click(object sender, RoutedEventArgs e) => AddNewTab(home: true);

    // ----- Updates --------------------------------------------------------

    private UpdateInfo? _pendingUpdate;

    private async Task CheckForUpdatesAsync()
    {
        var result = await Updater.CheckAsync();
        switch (result.Status)
        {
            case UpdateStatus.Available:
                _pendingUpdate = result.Info!;
                UpdateText.Text = $"Crystal Browser {result.Info!.Version} is available — you have {Config.Version}.";
                UpdateBar.Visibility = Visibility.Visible;
                _updateTimer.Stop(); // found it — stop pinging
                break;
            case UpdateStatus.UpToDate:
                _updateTimer.Stop(); // already latest — stop pinging entirely until next launch
                break;
            case UpdateStatus.Failed:
                break; // no answer (offline / not ready) — keep pinging constantly
        }
    }

    // Manual "Check for updates" from the Settings page. Unlike the passive banner, this
    // actively downloads the installer and launches it so the user doesn't have to hunt it down.
    private async Task ManualCheckForUpdatesAsync(CoreWebView2 core)
    {
        await ReportUpdate(core, "checking");
        var result = await Updater.CheckAsync();
        if (result.Status == UpdateStatus.UpToDate)
        {
            _updateTimer.Stop();             // definitively latest — stop pinging
            await ReportUpdate(core, "current");
            return;
        }
        if (result.Status == UpdateStatus.Failed)
        {
            await ReportUpdate(core, "error"); // couldn't reach GitHub — leave the poller running
            return;
        }

        var info = result.Info!;
        _pendingUpdate = info;
        UpdateText.Text = $"Crystal Browser {info.Version} is available — you have {Config.Version}.";
        UpdateBar.Visibility = Visibility.Visible;
        _updateTimer.Stop();

        // Show the version + a direct link, then download the installer right away.
        await ReportUpdate(core, "available", info.Version, info.PageUrl, info.DownloadUrl);
        await ReportUpdate(core, "downloading", info.Version);
        bool ok = await Updater.DownloadAndRunAsync(info);
        if (ok)
            Close(); // exit so the freshly-launched installer can replace files
        else
            await ReportUpdate(core, "failed", info.Version, info.PageUrl, info.DownloadUrl);
    }

    // Push an update status to the Settings page's crystalUpdateStatus(state, ver, page, dl) hook.
    private static async Task ReportUpdate(CoreWebView2 core, string state,
        string? version = null, string? pageUrl = null, string? downloadUrl = null)
    {
        static string J(string? s) => System.Text.Json.JsonSerializer.Serialize(s ?? "");
        await core.ExecuteScriptAsync(
            $"window.crystalUpdateStatus && window.crystalUpdateStatus({J(state)},{J(version)},{J(pageUrl)},{J(downloadUrl)})");
    }

    private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;
        BtnUpdateNow.IsEnabled = false;
        UpdateText.Text = "Downloading the update…";
        bool ok = await Updater.DownloadAndRunAsync(_pendingUpdate);
        if (ok)
            Close();          // exit so the installer can replace files
        else
        {
            UpdateText.Text = "Update download failed. Try again later.";
            BtnUpdateNow.IsEnabled = true;
        }
    }

    private void BtnUpdateLater_Click(object sender, RoutedEventArgs e) =>
        UpdateBar.Visibility = Visibility.Collapsed;

    // ----- Unsupported-version nag ----------------------------------------
    // End-of-life builds (see SupportPolicy) get a persistent reminder to update on
    // every launch. It never blocks the browser — "Later" just hides it for this
    // session and it returns next time, so users can keep using the old version.

    private void ShowUnsupportedNagIfNeeded()
    {
        if (SupportPolicy.IsUnsupported(Config.Version))
            UnsupportedBar.Visibility = Visibility.Visible;
    }

    private async void BtnUnsupportedUpdate_Click(object sender, RoutedEventArgs e)
    {
        BtnUnsupportedUpdate.IsEnabled = false;
        UnsupportedText.Text = "Checking for the latest version…";

        // Use the update the background poller already found, or check right now.
        var info = _pendingUpdate;
        if (info == null)
            info = (await Updater.CheckAsync()).Info;

        if (info == null)
        {
            UnsupportedText.Text = "Couldn't reach the update server. Please try again later.";
            BtnUnsupportedUpdate.IsEnabled = true;
            return;
        }

        UnsupportedText.Text = $"Downloading Crystal Browser {info.Version}…";
        bool ok = await Updater.DownloadAndRunAsync(info);
        if (ok)
            Close(); // exit so the installer can replace files
        else
        {
            UnsupportedText.Text = "Update download failed. Please try again later.";
            BtnUnsupportedUpdate.IsEnabled = true;
        }
    }

    private void BtnUnsupportedLater_Click(object sender, RoutedEventArgs e) =>
        UnsupportedBar.Visibility = Visibility.Collapsed;

    // ----- Default-browser nag (Chrome-style) -----------------------------

    private void ShowDefaultBrowserNag()
    {
        if (DefaultBrowser.ShouldNag())
            DefaultBar.Visibility = Visibility.Visible;
    }

    private void BtnSetDefault_Click(object sender, RoutedEventArgs e)
    {
        // Open Windows "Default apps" so the user can pick Crystal Browser, then hide
        // the banner for this session (it returns next launch if still not default).
        DefaultBrowser.OpenDefaultAppsSettings();
        DefaultBar.Visibility = Visibility.Collapsed;
    }

    private void BtnNoThanks_Click(object sender, RoutedEventArgs e)
    {
        // "No thanks" stops the nag for good, like dismissing Chrome's banner.
        DefaultBrowser.DismissNag();
        DefaultBar.Visibility = Visibility.Collapsed;
    }

    // ----- Private-window loading progress --------------------------------

    // Only private (Tor) windows get the percentage bar — normal pages load fast enough
    // that a progress meter would just flicker.
    private void StartLoadProgress()
    {
        if (!_incognito) return;
        _loadPct = 0;
        ApplyLoadPct();
        LoadBar.Visibility = Visibility.Visible;
        _loadTimer.Start();
    }

    // Ease toward (but never reach) 100% while the page is still loading, so the bar always
    // shows forward motion. NavigationCompleted snaps it to 100% and hides it.
    private void TickLoadProgress()
    {
        _loadPct += (95 - _loadPct) * 0.08;
        ApplyLoadPct();
    }

    private void FinishLoadProgress()
    {
        if (!_incognito) return;
        _loadTimer.Stop();
        _loadPct = 100;
        ApplyLoadPct();
        // Let the full bar register for a beat, then hide it.
        var hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        hide.Tick += (s, _) => { hide.Stop(); LoadBar.Visibility = Visibility.Collapsed; };
        hide.Start();
    }

    private void ApplyLoadPct()
    {
        LoadPct.Text = $"{_loadPct:0}%";
        LoadFill.Width = LoadTrack.ActualWidth * _loadPct / 100.0;
    }

    // ----- Bookmarks ------------------------------------------------------

    // ----- Screenshot tool ------------------------------------------------
    // Capture the visible page and drop a PNG into Documents\Crystal Browser Screens.

    private async void BtnShot_Click(object sender, RoutedEventArgs e)
    {
        var core = Current?.CoreWebView2;
        if (core == null) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Crystal Browser Screens");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"screenshot-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write))
                await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
            SetStatus($"Screenshot saved to {file}");
        }
        catch
        {
            SetStatus("Couldn't save the screenshot.");
        }
    }

    private void BtnStar_Click(object sender, RoutedEventArgs e)
    {
        var web = Current;
        if (web?.CoreWebView2 == null) return;
        var url = web.Source?.ToString() ?? "";
        if (string.IsNullOrEmpty(url) || url == "about:blank") return; // don't bookmark home/blank

        var title = string.IsNullOrWhiteSpace(web.CoreWebView2.DocumentTitle) ? url : web.CoreWebView2.DocumentTitle;
        _bookmarks.Toggle(url, title);
        RenderBookmarks();
        UpdateStar();
    }

    // Rebuild the bookmarks bar from the saved list.
    private void RenderBookmarks()
    {
        BookmarkStrip.Children.Clear();
        foreach (var bm in _bookmarks.Items)
        {
            // Redesigned chip (1.8.1): a rounded pill with a small accent dot + the title.
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8, Fill = (Brush)Resources["AccentBrush"],
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            };
            var label = new TextBlock
            {
                Text = Truncate(bm.Title, 24), Foreground = (Brush)Resources["TextPrimary"],
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(dot);
            content.Children.Add(label);
            var chip = new Button
            {
                Content = content, Style = (Style)FindResource("IconBtn"),
                Background = (Brush)Resources["SurfaceBg"],
                Width = double.NaN, Height = 30, Padding = new Thickness(12, 0, 14, 0),
                Margin = new Thickness(0, 0, 7, 0), Cursor = Cursors.Hand, ToolTip = bm.Url
            };
            chip.Click += (_, _) => { if (Current != null) Navigate(Current, bm.Url); };
            // Right-click removes the bookmark.
            chip.MouseRightButtonUp += (_, _) => { _bookmarks.Remove(bm.Url); RenderBookmarks(); UpdateStar(); };
            BookmarkStrip.Children.Add(chip);
        }
        BookmarkBar.Visibility = _bookmarks.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // Fill or outline the star depending on whether the current page is bookmarked.
    private void UpdateStar()
    {
        var url = Current?.Source?.ToString() ?? "";
        bool saved = !string.IsNullOrEmpty(url) && _bookmarks.Contains(url);
        BtnStar.Content = saved ? "★" : "☆"; // ★ : ☆
        BtnStar.Foreground = saved
            ? new SolidColorBrush(Color.FromRgb(0xff, 0xd1, 0x66))
            : new SolidColorBrush(Color.FromRgb(0xcf, 0xce, 0xeb));
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_active == null) { AddNewTab(home: true); }
        _active!.IsHome = false;
        _active.Title.Text = "Settings";
        _active.View.CoreWebView2?.NavigateToString(SettingsPage.Html(SettingsStore.Current.Theme));
    }

    // ----- AI companion sidebar (Gemini / Claude / ChatGPT) ---------------

    // The selectable AI companions, in sidebar order: key -> (display name, web app URL).
    // Gemini is first; Claude is last because it needs a prior sign-in (see below).
    private static readonly (string Key, string Name, string Url)[] AiCompanions =
    {
        ("gemini",  "Gemini",  "https://gemini.google.com/app"),
        ("chatgpt", "ChatGPT", "https://chatgpt.com/"),
        ("claude",  "Claude",  "https://claude.ai/new"),
    };

    private WebView2? _aiView;      // created lazily the first time the sidebar opens
    private bool _claudeUnlocked;   // cached: is the user signed in to claude.ai in this profile?

    private void BtnAi_Click(object sender, RoutedEventArgs e) => ToggleAiSidebar();
    private void BtnAiClose_Click(object sender, RoutedEventArgs e) => AiSidebar.Visibility = Visibility.Collapsed;

    private async void ToggleAiSidebar()
    {
        if (AiSidebar.Visibility == Visibility.Visible)
        {
            AiSidebar.Visibility = Visibility.Collapsed;
            return;
        }
        AiSidebar.Visibility = Visibility.Visible;
        await RefreshClaudeLockAsync();   // know whether Claude is usable before we render/navigate
        await EnsureAiViewAsync();
        ShowSelectedAi();
    }

    // Create the sidebar's own WebView2 (sharing the active profile, so AI logins persist).
    private async Task EnsureAiViewAsync()
    {
        if (_aiView != null) return;
        _aiView = new WebView2();
        AiHost.Children.Add(_aiView);
        var wc = (Color)ColorConverter.ConvertFromString(Theme.Get(SettingsStore.Current.Theme).WindowBg);
        _aiView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, wc.R, wc.G, wc.B);
        await _aiView.EnsureCoreWebView2Async(await GetEnvironmentAsync());
        // Open links the AI surfaces in a normal browser tab rather than a nested popup.
        _aiView.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; AddNewTab(url: e.Uri); };
    }

    // Is there a Claude session cookie in this profile? (Claude's auth cookie is "sessionKey".)
    private async Task<bool> IsClaudeSignedInAsync()
    {
        try
        {
            var core = _aiView?.CoreWebView2 ?? Current?.CoreWebView2;
            if (core == null) return false;
            var cookies = await core.CookieManager.GetCookiesAsync("https://claude.ai");
            return cookies.Any(c => c.Name.Contains("sessionKey", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private async Task RefreshClaudeLockAsync()
    {
        _claudeUnlocked = await IsClaudeSignedInAsync();
        RenderAiPicker();
    }

    // Show either the chosen AI's web view, or the "sign in to Claude" notice when Claude is
    // selected but locked.
    private void ShowSelectedAi()
    {
        var key = SettingsStore.Current.AiCompanion;
        if (key == "claude" && !_claudeUnlocked) { ShowClaudeNotice(true); return; }
        ShowClaudeNotice(false);
        NavigateAi();
    }

    private void ShowClaudeNotice(bool show)
    {
        AiNotice.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (_aiView != null) _aiView.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void NavigateAi()
    {
        var url = AiCompanions.FirstOrDefault(a => a.Key == SettingsStore.Current.AiCompanion).Url
                  ?? AiCompanions[0].Url;
        _aiView?.CoreWebView2?.Navigate(url);
    }

    private async void SetAi(string key)
    {
        // Claude is gated behind a prior sign-in: if it's locked, gently prompt instead of
        // dropping the user onto Claude's sign-in page, and don't switch to it.
        if (key == "claude")
        {
            await RefreshClaudeLockAsync();
            if (!_claudeUnlocked) { ShowClaudeNotice(true); return; }
        }
        SettingsStore.Current.AiCompanion = key;
        SettingsStore.Save();
        RenderAiPicker();
        await EnsureAiViewAsync();
        ShowClaudeNotice(false);
        NavigateAi();
    }

    private void BtnOpenClaude_Click(object sender, RoutedEventArgs e) =>
        AddNewTab(url: "https://claude.ai/login");

    private async void BtnRecheckClaude_Click(object sender, RoutedEventArgs e)
    {
        await RefreshClaudeLockAsync();
        if (_claudeUnlocked) SetAi("claude"); // now signed in — switch to Claude for real
        // still locked: leave the notice up so they can finish signing in.
    }

    // Rebuild the Gemini / ChatGPT / Claude switcher. Claude is dimmed and not selectable as the
    // active chip until the user is signed in; clicking it then shows the sign-in notice.
    private void RenderAiPicker()
    {
        AiPicker.Children.Clear();
        var active = SettingsStore.Current.AiCompanion;
        var accent = (Brush)Resources["AccentBrush"];
        foreach (var (key, name, _) in AiCompanions)
        {
            bool locked = key == "claude" && !_claudeUnlocked;
            bool on = key == active && !locked;
            var btn = new Button
            {
                Content = name, Cursor = Cursors.Hand, FontSize = 13,
                Foreground = on ? Brushes.White : (Brush)Resources["TabTextInactive"],
                Background = on ? accent : Brushes.Transparent,
                BorderThickness = new Thickness(0), Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 6, 0), Opacity = locked ? 0.5 : 1.0,
                ToolTip = locked ? "Sign in to Claude first to use it here" : null,
                Template = (ControlTemplate)FindResource("AiChipTemplate")
            };
            btn.Click += (_, _) => SetAi(key);
            AiPicker.Children.Add(btn);
        }
    }

    // ----- Crystal autofill -----------------------------------------------
    // Saved logins live in the active profile, encrypted at rest (see AutofillStore). The
    // injected script below watches every page for login forms: it asks the host to fill
    // known credentials on load, and offers to save them when a login form is submitted.

    // Pending credentials awaiting the user's "Save" decision (set when a login form is submitted).
    private (string Origin, string Username, string Password)? _pendingLogin;

    // Injected into every page (normal windows only). Posts JSON strings the host understands,
    // and exposes window.crystalFill(user, pass) for the host to call back and fill the form.
    private const string AutofillScript = """
(function () {
  if (window.__crystalAutofill) return; window.__crystalAutofill = true;
  function pwFields() { return Array.prototype.slice.call(document.querySelectorAll('input[type=password]')); }
  function userField(pw) {
    // The text/email field most likely to be the username: the visible one just before the password.
    var inputs = Array.prototype.slice.call(document.querySelectorAll('input'));
    var idx = inputs.indexOf(pw), best = null;
    for (var i = idx - 1; i >= 0; i--) {
      var t = (inputs[i].type || 'text').toLowerCase();
      if (t === 'text' || t === 'email' || t === 'tel' || (inputs[i].tagName === 'INPUT' && !inputs[i].type)) { best = inputs[i]; break; }
    }
    return best || inputs.find(function (e) { var t = (e.type||'').toLowerCase(); return t==='email'||t==='text'; });
  }
  function setVal(el, v) {
    if (!el) return;
    var d = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
    d && d.set ? d.set.call(el, v) : (el.value = v);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }
  window.crystalFill = function (user, pass) {
    var pws = pwFields(); if (!pws.length) return;
    if (user) setVal(userField(pws[0]), user);
    setVal(pws[0], pass);
  };
  function post(o) { try { window.chrome.webview.postMessage(JSON.stringify(o)); } catch (e) {} }
  function requestFill() { if (pwFields().length) post({ type: 'autofill-request', origin: location.origin }); }
  function onSubmit() {
    var pws = pwFields(); if (!pws.length) return;
    var pw = pws[0].value || ''; if (!pw) return;
    var uf = userField(pws[0]);
    post({ type: 'autofill-submit', origin: location.origin, username: uf ? (uf.value || '') : '', password: pw });
  }
  document.addEventListener('submit', onSubmit, true);
  document.addEventListener('DOMContentLoaded', requestFill);
  // Also handle JS-driven logins where the button isn't a real form submit.
  document.addEventListener('click', function (e) {
    var t = e.target; if (!t) return;
    var btn = t.closest && t.closest('button, input[type=submit], [role=button]');
    if (btn) setTimeout(onSubmit, 0);
  }, true);
  if (document.readyState !== 'loading') requestFill();
})();
""";

    // The page asked for any saved login for its origin — fill it (matching the typed username if any).
    private async Task HandleAutofillRequest(JsonElement root, CoreWebView2 core)
    {
        if (_autofill == null) return;
        var origin = root.TryGetProperty("origin", out var o) ? o.GetString() : null;
        if (string.IsNullOrEmpty(origin)) return;
        var login = _autofill.Match(origin);
        if (login == null) return;
        var pass = _autofill.Reveal(login);
        if (string.IsNullOrEmpty(pass)) return;
        string J(string s) => JsonSerializer.Serialize(s);
        await core.ExecuteScriptAsync($"window.crystalFill && window.crystalFill({J(login.Username)},{J(pass)})");
    }

    // A login form was submitted — offer to save it, unless we already have these exact credentials.
    private void HandleAutofillSubmit(JsonElement root)
    {
        if (_autofill == null) return;
        var origin = root.TryGetProperty("origin", out var o) ? o.GetString() : null;
        var username = root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
        var password = root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(password)) return;
        if (_autofill.AlreadySaved(origin!, username, password)) return; // nothing new to save

        _pendingLogin = (origin!, username, password);
        var host = origin!.Replace("https://", "").Replace("http://", "");
        SaveLoginText.Text = string.IsNullOrEmpty(username)
            ? $"Save this password for {host}?"
            : $"Save password for {username} on {host}?";
        SaveLoginBar.Visibility = Visibility.Visible;
    }

    private void BtnSaveLogin_Click(object sender, RoutedEventArgs e)
    {
        if (_autofill != null && _pendingLogin is { } pl)
        {
            _autofill.Upsert(pl.Origin, pl.Username, pl.Password);
            SetStatus("Password saved to Crystal autofill.");
        }
        _pendingLogin = null;
        SaveLoginBar.Visibility = Visibility.Collapsed;
    }

    private void BtnSaveLoginDismiss_Click(object sender, RoutedEventArgs e)
    {
        _pendingLogin = null;
        SaveLoginBar.Visibility = Visibility.Collapsed;
    }

    // ----- Edit mode (document.designMode) --------------------------------

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (Current?.CoreWebView2 == null) return;
        bool on = BtnEdit.IsChecked == true;
        await ApplyEditMode(Current, on);
        SetStatus(on ? "Edit mode ON — click into the page and start typing." : "Edit mode off.");
    }

    private static async Task ApplyEditMode(WebView2 web, bool on)
    {
        if (web.CoreWebView2 == null) return;
        var js = on
            ? "document.designMode='on'; document.body && document.body.focus();"
            : "document.designMode='off';";
        await web.CoreWebView2.ExecuteScriptAsync(js);
    }

    private async void ReapplyEditMode(WebView2 web)
    {
        if (BtnEdit.IsChecked == true) await ApplyEditMode(web, true);
    }

    // ----- Chrome sync ----------------------------------------------------

    private void SyncChrome()
    {
        var web = Current;
        if (web?.CoreWebView2 == null) return;
        AddressBar.Text = (_active?.IsHome == true) ? "" : Prettify(web.Source?.ToString() ?? "");
        BtnBack.IsEnabled = web.CanGoBack;
        BtnForward.IsEnabled = web.CanGoForward;
        UpdateStar();
    }

    private static string Prettify(string url)
    {
        if (string.IsNullOrEmpty(url) || url == "about:blank") return "";
        // Show the typed query (not the full URL) for search result pages.
        if (url.StartsWith("https://www.google.com/search", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://duckduckgo.com/?", StringComparison.OrdinalIgnoreCase))
        {
            var query = new Uri(url).Query.TrimStart('?');
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "q" && kv[1].Length > 0)
                    return Uri.UnescapeDataString(kv[1].Replace('+', ' '));
            }
        }
        return url;
    }

    private void SetStatus(string text) => StatusText.Text = text;
    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    // ----- System monitor -------------------------------------------------

    private void UpdateSystemStats()
    {
        var s = _monitor.Sample();

        CpuBar.Width = 60 * s.CpuPercent / 100.0;
        CpuText.Text = $"{s.CpuPercent:0}%";
        RamBar.Width = 60 * s.RamPercent / 100.0;
        RamText.Text = $"{s.RamPercent:0}%";

        // Feed the home page's live widget. crystalStats only exists on the home page,
        // so this is a harmless no-op on any other page — no need to gate on IsHome.
        if (Current?.CoreWebView2 != null)
        {
            var json = $"{{cpu:{s.CpuPercent:0.0},cores:{s.Cores},ramPct:{s.RamPercent:0.0}," +
                       $"ramUsed:{s.RamUsedGb:0.0},ramTotal:{s.RamTotalGb:0.0},appMem:{s.AppMemoryMb:0}}}";
            _ = Current.CoreWebView2.ExecuteScriptAsync(
                $"window.crystalStats && window.crystalStats({json})");
        }
    }
}

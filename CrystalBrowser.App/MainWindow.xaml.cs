using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    // Private (Tor) mode: this window routes through the Tor SOCKS proxy, isolated.
    private readonly bool _tor;
    private readonly int _torPort;            // 0 = Tor not detected on this machine
    private readonly string? _privateDataDir; // ephemeral profile folder for private windows

    private readonly BookmarkStore _bookmarks = new();
    private readonly SystemMonitor _monitor = new();
    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    // Private-window loading bar: a simulated percentage that climbs while a page loads over
    // Tor (which gives no real progress events), so the user can see it's actually working.
    private readonly DispatcherTimer _loadTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private double _loadPct;

    // A URL to open on launch instead of the home page (set when Windows starts us as the
    // default browser and hands us a link to open).
    private readonly string? _initialUrl;

    public MainWindow() : this(tor: false) { }

    public MainWindow(string url) : this(tor: false) { _initialUrl = url; }

    public MainWindow(bool tor)
    {
        InitializeComponent();
        _tor = tor;
        if (_tor)
        {
            _torPort = TorManager.DetectPort();
            _privateDataDir = Path.Combine(Path.GetTempPath(), "CrystalBrowserPrivate", Guid.NewGuid().ToString("N"));
            Title = "Crystal Browser — Private (Tor)";
            PrivateBadge.Visibility = Visibility.Visible;
            BtnPrivate.Visibility = Visibility.Collapsed; // no nested private windows
        }
        _statsTimer.Tick += (_, _) => UpdateSystemStats();
        _statsTimer.Start();
        _loadTimer.Tick += (_, _) => TickLoadProgress();
        Closed += (_, _) => { _loadTimer.Stop(); _monitor.Dispose(); CleanupPrivateProfile(); };
        StateChanged += OnStateChanged;
        Loaded += (_, _) =>
        {
            FitToScreen();
            RenderBookmarks();
            if (!string.IsNullOrWhiteSpace(_initialUrl))
                AddNewTab(url: _initialUrl);
            else
                AddNewTab(home: true);
            if (!_tor)
            {
                _ = CheckForUpdatesAsync(); // only the normal window checks
                ShowDefaultBrowserNag();    // Chrome-style "set me as default" banner
            }
        };
    }

    private void CleanupPrivateProfile()
    {
        if (_privateDataDir != null && Directory.Exists(_privateDataDir))
            try { Directory.Delete(_privateDataDir, recursive: true); } catch { }
    }

    private async void BtnPrivate_Click(object sender, RoutedEventArgs e)
    {
        BtnPrivate.IsEnabled = false;
        SetStatus("Starting Tor… (first connection can take a moment)");
        // Launch the bundled Tor and wait for its circuit before opening the window,
        // so the private window detects the live SOCKS port on startup.
        await TorManager.Instance.EnsureStartedAsync();
        SetStatus("Ready");
        BtnPrivate.IsEnabled = true;
        new MainWindow(tor: true).Show();
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
    }

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
        var args = "--enable-features=WebContentsForceDark";
        // Private windows tunnel through Tor's SOCKS proxy. Chromium resolves DNS remotely
        // over SOCKS5, so hostnames go through Tor too (no DNS leak).
        if (_tor && _torPort > 0)
            args += $" --proxy-server=socks5://127.0.0.1:{_torPort}";
        var options = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = args };
        // Private windows use an isolated, ephemeral profile folder; normal windows use the default.
        _env = await CoreWebView2Environment.CreateAsync(null, _privateDataDir, options);
        return _env;
    }

    private void AddNewTab(bool home = false, string? url = null)
    {
        var web = new WebView2 { Visibility = Visibility.Collapsed };
        BrowserHost.Children.Add(web);

        var title = new TextBlock
        {
            Text = "New Tab", Foreground = Brushes.White, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 150, TextTrimming = TextTrimming.CharacterEllipsis
        };
        var close = new Button
        {
            Content = "✕", Style = (Style)FindResource("TabClose"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0)
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 7, 6, 7) };
        panel.Children.Add(title);
        panel.Children.Add(close);
        var header = new Border
        {
            CornerRadius = new CornerRadius(10, 10, 0, 0), Cursor = Cursors.Hand,
            Margin = new Thickness(2, 4, 0, 0), Child = panel
        };

        // Header lives inside the WindowChrome caption strip — make it clickable.
        WindowChrome.SetIsHitTestVisibleInChrome(header, true);

        var tab = new BrowserTab { View = web, Header = header, Title = title };
        _tabs.Add(tab);
        TabHeaders.Items.Add(header);

        header.MouseLeftButtonUp += (_, _) => Activate(tab);
        close.Click += (s, e) => { e.Handled = true; CloseTab(tab); };

        Activate(tab);
        InitWebView(tab, home, url);
    }

    private void CloseTab(BrowserTab tab)
    {
        int idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TabHeaders.Items.Remove(tab.Header);
        BrowserHost.Children.Remove(tab.View);
        tab.View.Dispose();

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
            t.Header.Background = on
                ? new SolidColorBrush(Color.FromRgb(0x1b, 0x19, 0x33))
                : Brushes.Transparent;
            t.Title.Foreground = on
                ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xb9, 0xb7, 0xda));
        }
        SyncChrome();
    }

    private async void InitWebView(BrowserTab tab, bool home, string? url)
    {
        var web = tab.View;
        // Dark canvas behind every page so navigations don't flash blinding white.
        web.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x0E, 0x0D, 0x1C);
        await web.EnsureCoreWebView2Async(await GetEnvironmentAsync());
        var core = web.CoreWebView2;

        // Tell sites we prefer dark (Google etc. honour this natively); Chromium's
        // auto-dark engine (enabled via the env flag) darkens the rest.
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;

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

        if (home || url == null)
            GoHome(tab);
        else
            Navigate(web, url);
    }

    // ----- Home page ------------------------------------------------------

    private void GoHome(BrowserTab tab)
    {
        tab.IsHome = true;
        tab.Title.Text = "New Tab";
        // Private windows: show the Tor-bundled private home (or, if Tor isn't reachable,
        // explain how to start it). Normal windows get the regular home page.
        var html = _tor
            ? (_torPort == 0 ? PrivatePage.TorMissingHtml() : PrivatePage.PrivateHomeHtml())
            : HomePage.Html();
        tab.View.CoreWebView2?.NavigateToString(html);
        if (tab == _active) { AddressBar.Text = ""; AddressBar.Focus(); }
    }

    // ----- Navigation -----------------------------------------------------

    private void Navigate(WebView2 web, string input)
    {
        if (web.CoreWebView2 == null) return;
        web.CoreWebView2.Navigate(Resolve(input));
    }

    private static string Resolve(string input)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input)) return Config.SearchUrl;
        if (input.StartsWith("http://") || input.StartsWith("https://")) return input;
        if (!input.Contains(' ') && input.Contains('.') && !input.Contains('?')) return "https://" + input;
        return Config.SearchUrl + Uri.EscapeDataString(input);
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
        var info = await Updater.CheckAsync();
        if (info == null) return;
        _pendingUpdate = info;
        UpdateText.Text = $"Crystal Browser {info.Version} is available — you have {Config.Version}.";
        UpdateBar.Visibility = Visibility.Visible;
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
        if (!_tor) return;
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
        if (!_tor) return;
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
            var label = new TextBlock
            {
                Text = Truncate(bm.Title, 24), Foreground = new SolidColorBrush(Color.FromRgb(0xcf, 0xce, 0xeb)),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center
            };
            var chip = new Button
            {
                Content = label, Style = (Style)FindResource("IconBtn"),
                Width = double.NaN, Height = 26, Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(2, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = bm.Url
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
        _active.View.CoreWebView2?.NavigateToString(SettingsPage.Html());
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
        // Show the typed query (not the full Google URL) for search result pages.
        if (url.StartsWith("https://www.google.com/search", StringComparison.OrdinalIgnoreCase))
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

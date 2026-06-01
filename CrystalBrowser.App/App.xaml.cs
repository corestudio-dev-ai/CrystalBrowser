using System.Windows;

namespace CrystalBrowser.App;

/// <summary>
/// Application entry point. Crystal Browser searches the live web via Google, so there is
/// no local backend to launch — the app is fully self-contained.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Open the main window — and, when Windows launches us as the default browser, navigate
    /// to the URL it passes on the command line (e.g. "CrystalBrowser.App.exe https://…").
    /// </summary>
    private void App_Startup(object sender, StartupEventArgs e)
    {
        var url = e.Args.FirstOrDefault(a => !a.StartsWith('-') && !a.StartsWith('/'));
        var window = string.IsNullOrWhiteSpace(url) ? new MainWindow() : new MainWindow(url);
        window.Show();
    }
}

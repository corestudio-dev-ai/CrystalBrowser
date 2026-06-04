using System.IO;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>User-configurable settings, persisted as JSON under %AppData%\CrystalBrowser.</summary>
public class AppSettings
{
    /// <summary>"horizontal" (default) or "vertical" tab strip.</summary>
    public string TabLayout { get; set; } = "horizontal";

    /// <summary>On startup: "newtab" (home page) or "url" (open <see cref="StartupUrl"/>).</summary>
    public string Startup { get; set; } = "newtab";

    /// <summary>The page to open at startup when <see cref="Startup"/> is "url".</summary>
    public string StartupUrl { get; set; } = "";

    /// <summary>Accent colour (hex) used to theme the UI.</summary>
    public string Accent { get; set; } = "#7C6CFF";

    /// <summary>UI theme key: "dark" (Crystal, default), "light", "midnight", "forest" or "rose".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Search engine key: "google" (default) or "duckduckgo".</summary>
    public string SearchEngine { get; set; } = "google";

    /// <summary>AI companion shown in the sidebar: "gemini" (default), "chatgpt" or "claude".</summary>
    public string AiCompanion { get; set; } = "gemini";

    /// <summary>Name of the active Crystal profile.</summary>
    public string ActiveProfile { get; set; } = "Default";

    /// <summary>True once the first-run onboarding flow has been completed (or skipped).</summary>
    public bool OnboardingDone { get; set; } = false;
}

/// <summary>Loads and saves the single shared <see cref="AppSettings"/> instance.</summary>
public static class SettingsStore
{
    private static readonly string Path_ = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrystalBrowser", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path_))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path_)) ?? new();
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path_)!);
            File.WriteAllText(Path_, JsonSerializer.Serialize(Current,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best effort */ }
    }
}

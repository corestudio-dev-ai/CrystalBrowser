using System.IO;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>User-configurable settings, persisted as JSON under %AppData%\CrystalBrowser.</summary>
public class AppSettings
{
    /// <summary>"vertical" (default, Zen-style) or "horizontal" tab strip.</summary>
    public string TabLayout { get; set; } = "vertical";

    /// <summary>On startup: "newtab" (home page) or "url" (open <see cref="StartupUrl"/>).</summary>
    public string Startup { get; set; } = "newtab";

    /// <summary>The page to open at startup when <see cref="Startup"/> is "url".</summary>
    public string StartupUrl { get; set; } = "";

    /// <summary>Accent colour (hex) used to theme the UI. Defaults to Ultra blue (2.0).</summary>
    public string Accent { get; set; } = "#2F6BFF";

    /// <summary>Window frame border colour (hex), user-changeable in Settings.</summary>
    public string FrameColor { get; set; } = "#2F6BFF";

    /// <summary>UI theme key: "aero" (default see-through glass) or "light". Crystal is
    /// light-only as of 2.1 — old "dark"/"ultra" values migrate to "aero" on launch.</summary>
    public string Theme { get; set; } = "aero";

    /// <summary>Search engine key: "google" (default) or "duckduckgo".</summary>
    public string SearchEngine { get; set; } = "google";

    /// <summary>AI companion shown in the sidebar: "gemini" (default), "chatgpt" or "claude".</summary>
    public string AiCompanion { get; set; } = "gemini";

    /// <summary>Name of the active Crystal profile.</summary>
    public string ActiveProfile { get; set; } = "Default";

    /// <summary>True once the first-run onboarding flow has been completed (or skipped).</summary>
    public bool OnboardingDone { get; set; } = false;

    /// <summary>The app version whose "what's new" changelog the user has already seen. When this
    /// differs from <see cref="Config.Version"/> after an update, Crystal shows the changelog.</summary>
    public string LastSeenVersion { get; set; } = "";
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

    /// <summary>Restore all settings to their defaults (deletes settings.json). Used by the
    /// "Reset Crystal" action, which then restarts the app so defaults take effect cleanly.</summary>
    public static void Reset()
    {
        try { if (File.Exists(Path_)) File.Delete(Path_); } catch { /* best effort */ }
        Current = new AppSettings();
    }
}

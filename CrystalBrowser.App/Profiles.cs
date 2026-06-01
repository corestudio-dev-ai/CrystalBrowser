using System.IO;
using System.Text;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>
/// Crystal profiles. Each profile keeps its own isolated WebView2 data folder (cookies,
/// logins, cache, history), so different profiles browse independently. The profile list is
/// persisted; the active profile lives in <see cref="AppSettings.ActiveProfile"/>.
/// </summary>
public static class ProfileStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrystalBrowser");
    private static readonly string Path_ = Path.Combine(Dir, "profiles.json");

    public static List<string> Items { get; private set; } = Load();

    private static List<string> Load()
    {
        try
        {
            if (File.Exists(Path_))
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(Path_));
                if (list is { Count: > 0 }) return list;
            }
        }
        catch { /* fall through */ }
        return new List<string> { "Default" };
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path_, JsonSerializer.Serialize(Items));
        }
        catch { /* best effort */ }
    }

    /// <summary>Add a new profile (no-op if the name already exists). Returns true if added.</summary>
    public static bool Add(string name)
    {
        name = name.Trim();
        if (string.IsNullOrEmpty(name) ||
            Items.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
            return false;
        Items.Add(name);
        Save();
        return true;
    }

    /// <summary>
    /// Make sure the active profile actually exists; if it was renamed/removed, fall back to
    /// Default (creating it if needed). Cements profiles as the always-present storage unit.
    /// </summary>
    public static void EnsureActive()
    {
        if (!Items.Any(p => p == "Default")) { Items.Insert(0, "Default"); Save(); }
        var active = SettingsStore.Current.ActiveProfile;
        if (!Items.Any(p => string.Equals(p, active, StringComparison.OrdinalIgnoreCase)))
        {
            SettingsStore.Current.ActiveProfile = "Default";
            SettingsStore.Save();
        }
    }

    /// <summary>The signed-in (synced) account email for a profile, or null if not signed in.</summary>
    public static string? AccountEmail(string profile)
    {
        try
        {
            var p = Path.Combine(DataDir(profile), "account.json");
            if (File.Exists(p))
            {
                var a = JsonSerializer.Deserialize<Account>(File.ReadAllText(p));
                return string.IsNullOrWhiteSpace(a?.Email) ? null : a!.Email;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>Sign a profile in (email) or out (null), persisted in the profile folder.</summary>
    public static void SetAccountEmail(string profile, string? email)
    {
        try
        {
            File.WriteAllText(Path.Combine(DataDir(profile), "account.json"),
                JsonSerializer.Serialize(new Account { Email = email }));
        }
        catch { /* best effort */ }
    }

    private sealed class Account { public string? Email { get; set; } }

    /// <summary>The isolated WebView2 user-data folder for a profile.</summary>
    public static string DataDir(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (string.IsNullOrEmpty(safe)) safe = "Default";
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrystalBrowser", "Profiles", safe);
        Directory.CreateDirectory(dir);
        return dir;
    }
}

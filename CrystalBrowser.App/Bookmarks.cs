using System.IO;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>A single saved bookmark.</summary>
public record Bookmark(string Title, string Url);

/// <summary>
/// Persistent bookmark list, stored per Crystal profile under that profile's data folder
/// (so each profile keeps its own bookmarks, just like its history). Falls back to importing
/// the pre-1.5.3 global bookmarks for the Default profile.
/// </summary>
public class BookmarkStore
{
    private readonly string _path;
    public List<Bookmark> Items { get; private set; } = new();

    public BookmarkStore(string profile)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CrystalBrowser", "Profiles", Sanitize(profile));
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "bookmarks.json");
        Load(profile);
    }

    private static string Sanitize(string name)
    {
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return string.IsNullOrEmpty(safe) ? "Default" : safe;
    }

    private void Load(string profile)
    {
        try
        {
            if (File.Exists(_path))
            {
                Items = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(_path)) ?? new();
                return;
            }
            // One-time import of the old global bookmarks into the Default profile.
            if (string.Equals(profile, "Default", StringComparison.OrdinalIgnoreCase))
            {
                var legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CrystalBrowser", "bookmarks.json");
                if (File.Exists(legacy))
                {
                    Items = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(legacy)) ?? new();
                    Save();
                }
            }
        }
        catch { Items = new(); }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Items)); }
        catch { /* best effort */ }
    }

    public bool Contains(string url) =>
        Items.Any(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));

    /// <summary>Add the page if new, or remove it if already bookmarked. Returns the new state.</summary>
    public bool Toggle(string url, string title)
    {
        var existing = Items.FirstOrDefault(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing != null) { Items.Remove(existing); Save(); return false; }
        Items.Add(new Bookmark(string.IsNullOrWhiteSpace(title) ? url : title, url));
        Save();
        return true;
    }

    public void Remove(string url)
    {
        Items.RemoveAll(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    /// <summary>Bulk-add bookmarks (skipping ones already saved), persisting once. Returns how many were added.</summary>
    public int Import(IEnumerable<Bookmark> incoming)
    {
        int added = 0;
        foreach (var b in incoming)
        {
            if (string.IsNullOrWhiteSpace(b.Url) || Contains(b.Url)) continue;
            Items.Add(new Bookmark(string.IsNullOrWhiteSpace(b.Title) ? b.Url : b.Title, b.Url));
            added++;
        }
        if (added > 0) Save();
        return added;
    }
}

using System.IO;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>A single saved bookmark.</summary>
public record Bookmark(string Title, string Url);

/// <summary>
/// Persistent bookmark list, stored as JSON under %AppData%\CrystalBrowser\bookmarks.json.
/// </summary>
public class BookmarkStore
{
    private readonly string _path;
    public List<Bookmark> Items { get; private set; } = new();

    public BookmarkStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrystalBrowser");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "bookmarks.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                Items = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(_path)) ?? new();
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
}

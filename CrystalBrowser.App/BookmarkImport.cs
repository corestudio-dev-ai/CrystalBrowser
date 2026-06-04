using System.IO;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>
/// Reads bookmarks out of an installed Chromium-based browser (Chrome, Edge, Brave) so the
/// first-run onboarding can import them. Each of those browsers stores its bookmarks as a JSON
/// file ("Bookmarks") in the Default profile folder, in the same well-known tree shape.
/// </summary>
public static class BookmarkImport
{
    /// <summary>An installed browser Crystal can import bookmarks from.</summary>
    public record Source(string Id, string Name, string Path);

    private static readonly (string Id, string Name, string[] Rel)[] Candidates =
    {
        ("chrome", "Google Chrome", new[] { "Google", "Chrome", "User Data", "Default", "Bookmarks" }),
        ("edge",   "Microsoft Edge", new[] { "Microsoft", "Edge", "User Data", "Default", "Bookmarks" }),
        ("brave",  "Brave",          new[] { "BraveSoftware", "Brave-Browser", "User Data", "Default", "Bookmarks" }),
    };

    /// <summary>The installed browsers whose bookmarks file actually exists on this machine.</summary>
    public static List<Source> Available()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var found = new List<Source>();
        foreach (var (id, name, rel) in Candidates)
        {
            var path = Path.Combine(new[] { local }.Concat(rel).ToArray());
            if (File.Exists(path)) found.Add(new Source(id, name, path));
        }
        return found;
    }

    /// <summary>Parse a Chromium "Bookmarks" file into a flat list of (title, url) entries.</summary>
    public static List<Bookmark> Read(string path)
    {
        var list = new List<Bookmark>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("roots", out var roots))
                foreach (var root in roots.EnumerateObject())
                    Walk(root.Value, list);
        }
        catch { /* malformed / unreadable — return whatever we gathered */ }
        return list;
    }

    // Recurse the bookmark tree, collecting every "url" node.
    private static void Walk(JsonElement node, List<Bookmark> into)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        var type = node.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type == "url")
        {
            var url = node.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (!string.IsNullOrWhiteSpace(url) &&
                (url.StartsWith("http://") || url.StartsWith("https://")))
            {
                var name = node.TryGetProperty("name", out var n) ? n.GetString() : null;
                into.Add(new Bookmark(string.IsNullOrWhiteSpace(name) ? url : name!, url!));
            }
        }
        else if (node.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in kids.EnumerateArray()) Walk(child, into);
        }
    }
}

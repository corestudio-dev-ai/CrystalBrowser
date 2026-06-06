using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrystalBrowser.App;

/// <summary>One saved login: a site origin plus the credentials Crystal will autofill there.</summary>
public sealed class SavedLogin
{
    /// <summary>The site origin this login belongs to, e.g. "https://github.com".</summary>
    public string Origin { get; set; } = "";
    public string Username { get; set; } = "";
    /// <summary>The password, DPAPI-protected (CurrentUser) and base64-encoded on disk.</summary>
    public string PasswordProtected { get; set; } = "";
}

/// <summary>
/// Crystal's own autofill engine ("Crystal autofill"). Saved logins live per Crystal profile
/// under that profile's data folder (logins.json), next to its bookmarks. Passwords are encrypted
/// at rest with Windows DPAPI scoped to the current user, so the file is useless if copied to
/// another account or machine. Plaintext passwords only ever exist in memory while filling a form.
/// Follows the same best-effort load/save pattern as the other stores (never throws).
/// </summary>
public sealed class AutofillStore
{
    private readonly string _path;
    private List<SavedLogin> _items = new();

    public AutofillStore(string profile)
    {
        var dir = ProfileStore.DataDir(profile);
        _path = Path.Combine(dir, "logins.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _items = JsonSerializer.Deserialize<List<SavedLogin>>(File.ReadAllText(_path)) ?? new();
        }
        catch { _items = new(); }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true })); }
        catch { /* best effort */ }
    }

    /// <summary>The saved logins (passwords are still encrypted in <see cref="SavedLogin.PasswordProtected"/>).</summary>
    public IReadOnlyList<SavedLogin> Items => _items;

    /// <summary>Normalise a URL to its origin (scheme://host[:port]); null for non-web/blank URLs.</summary>
    public static string? OriginOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var u = new Uri(url);
            if (u.Scheme != "http" && u.Scheme != "https") return null;
            return u.IsDefaultPort ? $"{u.Scheme}://{u.Host}" : $"{u.Scheme}://{u.Host}:{u.Port}";
        }
        catch { return null; }
    }

    private static string Protect(string plain)
    {
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch { return ""; }
    }

    private static string Unprotect(string protectedB64)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedB64), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    /// <summary>The decrypted password for a saved login, or "" if it can't be read.</summary>
    public string Reveal(SavedLogin login) => Unprotect(login.PasswordProtected);

    /// <summary>The best saved credential for an origin (matching username if given), or null.</summary>
    public SavedLogin? Match(string origin, string? username = null)
    {
        var hits = _items.Where(l => string.Equals(l.Origin, origin, StringComparison.OrdinalIgnoreCase)).ToList();
        if (hits.Count == 0) return null;
        if (!string.IsNullOrEmpty(username))
        {
            var exact = hits.FirstOrDefault(l => string.Equals(l.Username, username, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
        }
        return hits[0];
    }

    /// <summary>Save a new login or update the password of an existing (origin, username) pair.</summary>
    public void Upsert(string origin, string username, string password)
    {
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(password)) return;
        var existing = _items.FirstOrDefault(l =>
            string.Equals(l.Origin, origin, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.Username, username, StringComparison.OrdinalIgnoreCase));
        if (existing != null) existing.PasswordProtected = Protect(password);
        else _items.Add(new SavedLogin { Origin = origin, Username = username, PasswordProtected = Protect(password) });
        Save();
    }

    /// <summary>True if we already have this exact origin/username/password (so no save prompt is needed).</summary>
    public bool AlreadySaved(string origin, string username, string password)
    {
        var match = _items.FirstOrDefault(l =>
            string.Equals(l.Origin, origin, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.Username, username, StringComparison.OrdinalIgnoreCase));
        return match != null && Unprotect(match.PasswordProtected) == password;
    }

    public void Remove(string origin, string username)
    {
        _items.RemoveAll(l =>
            string.Equals(l.Origin, origin, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(l.Username, username, StringComparison.OrdinalIgnoreCase));
        Save();
    }
}

using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace CrystalBrowser.App;

/// <summary>
/// Manages the bundled Tor process. Private windows route through Tor's SOCKS proxy;
/// this class locates the shipped tor.exe, starts it on demand, waits for it to bootstrap,
/// and shuts it down when the app exits. If the user already runs Tor (e.g. Tor Browser),
/// that existing instance is reused instead.
/// </summary>
public sealed class TorManager
{
    public static readonly TorManager Instance = new();

    public const int SocksPort = 9050;       // port the bundled Tor listens on
    private Process? _proc;
    private readonly object _lock = new();

    static TorManager()
    {
        // Make sure Tor is stopped when the app process ends.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Instance.Stop();
    }

    /// <summary>
    /// Ensure a Tor SOCKS proxy is reachable. Returns the live port, or 0 if Tor could not
    /// be started (e.g. the bundle is missing). Safe to call repeatedly.
    /// </summary>
    public async Task<int> EnsureStartedAsync()
    {
        // Already running (ours or the user's Tor Browser on 9050/9150)?
        int existing = DetectPort();
        if (existing > 0) return existing;

        var exe = LocateTorExe();
        if (exe == null) return 0;

        lock (_lock)
        {
            if (_proc is { HasExited: false }) { /* starting already */ }
            else StartProcess(exe);
        }

        // Tor takes a few seconds to bootstrap its circuit; poll the SOCKS port.
        for (int i = 0; i < 60; i++)
        {
            if (PortOpen(SocksPort)) return SocksPort;
            await Task.Delay(500);
        }
        return PortOpen(SocksPort) ? SocksPort : 0;
    }

    private void StartProcess(string exe)
    {
        var torRoot = Directory.GetParent(Path.GetDirectoryName(exe)!)!.FullName; // ...\tor
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrystalBrowser", "tor-data");
        Directory.CreateDirectory(dataDir);

        var geoip = Path.Combine(torRoot, "data", "geoip");
        var geoip6 = Path.Combine(torRoot, "data", "geoip6");

        var args = $"--SocksPort {SocksPort} --DataDirectory \"{dataDir}\"";
        if (File.Exists(geoip)) args += $" --GeoIPFile \"{geoip}\"";
        if (File.Exists(geoip6)) args += $" --GeoIPv6File \"{geoip6}\"";

        _proc = Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    public void Stop()
    {
        lock (_lock)
        {
            try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); }
            catch { }
            _proc = null;
        }
    }

    // Find the shipped tor.exe by walking up from the running binary: <root>\tor\tor\tor.exe
    private static string? LocateTorExe()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tor", "tor", "tor.exe");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Detect a reachable Tor SOCKS port (bundled 9050, or Tor Browser's 9150).</summary>
    public static int DetectPort()
    {
        foreach (var port in new[] { SocksPort, 9150 })
            if (PortOpen(port)) return port;
        return 0;
    }

    private static bool PortOpen(int port)
    {
        try
        {
            using var c = new TcpClient();
            return c.ConnectAsync("127.0.0.1", port).Wait(400) && c.Connected;
        }
        catch { return false; }
    }
}

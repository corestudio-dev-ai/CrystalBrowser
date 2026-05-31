using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CrystalBrowser.App;

/// <summary>
/// Lightweight live system telemetry: CPU load, RAM usage, and the browser's own
/// memory footprint. Sampled on demand (cheap) for display in the status bar and home page.
/// </summary>
public sealed class SystemMonitor : IDisposable
{
    private readonly PerformanceCounter _cpu;
    private readonly Process _self = Process.GetCurrentProcess();

    public SystemMonitor()
    {
        _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
        _cpu.NextValue(); // first read primes the counter (always 0)
    }

    public SystemStats Sample()
    {
        var mem = new MEMORYSTATUSEX();
        GlobalMemoryStatusEx(mem);

        double cpu = Math.Clamp(_cpu.NextValue(), 0, 100);
        double ramPct = mem.dwMemoryLoad;
        double ramUsedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / 1073741824.0;
        double ramTotalGb = mem.ullTotalPhys / 1073741824.0;

        _self.Refresh();
        double appMb = _self.WorkingSet64 / 1048576.0;

        return new SystemStats(cpu, ramPct, ramUsedGb, ramTotalGb, appMb,
            Environment.ProcessorCount);
    }

    public void Dispose() => _cpu.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

/// <param name="CpuPercent">Total CPU utilisation 0–100.</param>
/// <param name="RamPercent">System memory in use 0–100.</param>
/// <param name="RamUsedGb">Physical RAM used, in GB.</param>
/// <param name="RamTotalGb">Total physical RAM, in GB.</param>
/// <param name="AppMemoryMb">Crystal Browser's own working set, in MB.</param>
/// <param name="Cores">Logical processor count.</param>
public readonly record struct SystemStats(
    double CpuPercent, double RamPercent, double RamUsedGb,
    double RamTotalGb, double AppMemoryMb, int Cores);

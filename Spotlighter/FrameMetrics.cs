using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Spotlighter;

// Opt-in end-to-end measurement. Fixed storage; no per-frame logging/allocation.
internal static class FrameMetrics
{
    internal static string? Output;
    internal static bool Idle,Interrupted;
    internal static int TestWidth, TestHeight;
    internal static string Adapter = "CPU/GDI";



    internal static string Mode = "CPU retained mask";
    private static long _allocatedStart;
    private static readonly double[] Cpu = new double[8192];
    private static int _count;
    private static long _started;
    private static TimeSpan _cpuStart;
    internal static double Milliseconds(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    internal static void Start()
    {
        _started = Stopwatch.GetTimestamp();
        using Process process = Process.GetCurrentProcess();
        _cpuStart = process.TotalProcessorTime;
        _allocatedStart = GC.GetTotalAllocatedBytes();
    }
    internal static void Record(double ms)
    {
        if (Output == null || _count >= Cpu.Length) return;
        Cpu[_count++] = ms;
    }
    private static string Stats(double[] data, int count)
    {
        if (count == 0) return "unavailable";
        double total = 0;
        for (int i = 0; i < count; i++) total += data[i];
        Array.Sort(data, 0, count);
        return string.Create(CultureInfo.InvariantCulture, $"mean={total / count:F3} ms; p95={data[(int)((count - 1) * .95)]:F3} ms; n={count}");
    }
    internal static void Save(IDesktopOverlay surface)
    {
        if (Output == null) return;
        long allocated=GC.GetTotalAllocatedBytes()-_allocatedStart;
        using Process process = Process.GetCurrentProcess();
        double seconds = Milliseconds(_started) / 1000;
        File.WriteAllText(Output, string.Create(CultureInfo.InvariantCulture,
            $"Interactive interruption: {Interrupted} (discard FPS if true)\nBackend: CPU cached mask\nAdapter: {Adapter}\nMode: {Mode}\nLogical lighting viewport: {surface.Width}x{surface.Height} at {surface.Left},{surface.Top}\nFull composition output / virtual desktop: {Native.GetSystemMetrics(78)}x{Native.GetSystemMetrics(79)}\nDuration: {seconds:F3} s; rendered={_count}; render submission FPS={_count / seconds:F2}\nRender wall time (mask + lamp + submission): {Stats(Cpu, _count)}\nCapture updates: 0 (no capture)\nNo custom GPU drawing per mouse frame. DWM compositor time/memory not measured.\nManaged allocation during interval: {allocated} bytes\nProcess CPU: {(process.TotalProcessorTime - _cpuStart).TotalSeconds / seconds * 100:F2}% of one logical core\nWorking set: {process.WorkingSet64 / 1048576.0:F2} MiB; private: {process.PrivateMemorySize64 / 1048576.0:F2} MiB\nMouse: {(Idle ? "fixed light position (idle check)" : "synthetic continuous trajectory")}; physical input-to-photon latency NOT measured.\nFPS counts application submissions, not physical display refreshes.\n"));
    }
}

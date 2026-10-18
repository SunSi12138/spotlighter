using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Spotlighter;

// One-shot high-resolution timer + message-aware wait, on the window thread.
// QPC deadlines avoid WM_TIMER's coarse scheduling and frame-skipping double gate.
internal sealed unsafe class FrameClock : IDisposable
{
    private nint _timer;
    private long _next;
    private Func<bool>? _active;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWaitableTimerExW(nint attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll")] private static extern bool SetWaitableTimer(nint timer, ref long due, int period, nint routine, nint argument, bool resume);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern uint MsgWaitForMultipleObjectsEx(uint count, nint* handles, uint timeout, uint mask, uint flags);
    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Native.MSG msg, nint hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool IsDialogMessageW(nint hwnd, ref Native.MSG msg);
    internal FrameClock()
    {
        _timer = CreateWaitableTimerExW(0, null, 2, 0x1F0003);
        if (_timer == 0) _timer = CreateWaitableTimerExW(0, null, 0, 0x1F0003);
        if (_timer == 0) throw new InvalidOperationException("Could not create frame timer");
        _next = Stopwatch.GetTimestamp(); Arm();
    }
    private void Arm()
    {
        long now = Stopwatch.GetTimestamp();
        long period=Stopwatch.Frequency/(_active?.Invoke()==false ? 4 : 60);
        _next += period;
        if (_next <= now) _next = now + period;
        long due = -Math.Max(1, (_next - now) * 10000000 / Stopwatch.Frequency);
        if (!SetWaitableTimer(_timer, ref due, 0, 0, 0, false)) throw new InvalidOperationException("Could not arm frame timer");
    }
    internal void Run(Action tick, Func<bool> active)
    {
        _active=active;
        while (true)
        {
            nint timer = _timer;
            uint result = MsgWaitForMultipleObjectsEx(1, &timer, uint.MaxValue, 0x04ff, 4);
            if (result == uint.MaxValue) throw new InvalidOperationException("Message wait failed");
            // Drain messages before drawing, including emergency disable and input.
            while (PeekMessageW(out Native.MSG msg, 0, 0, 0, 1))
            {
                if (msg.message == 0x12) return;
                Native.TranslateMessage(ref msg); Native.DispatchMessageW(ref msg);
            }
            if (result == 0) { tick(); Arm(); }
        }
    }
    public void Dispose() { if (_timer != 0) CloseHandle(_timer); _timer = 0; }
}

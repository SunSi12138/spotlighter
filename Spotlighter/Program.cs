using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spotlighter;

/// <summary>
/// Native AOT Win32 message pump. No WPF, WinForms, browser runtime or JIT.
/// Desktop lamp and tray controls. No keyboard or mouse hooks.
/// </summary>
internal static unsafe class Program
{
    private const string WindowClass = "Spotlighter.LightWindow.1";
    private const uint TrayMessage = Native.WM_USER + 27;
    private static LampOverlay? _lamp;
    private static nint _foreground;
    private static float _drawX=float.NaN,_drawY;
    private static LightSettings? _drawSettings;

    // Keep reverse P/Invoke delegates alive for the lifetime of the process.
    private static readonly Native.WndProc WindowCallback = WindowProc;

    private static nint _module, _messageWindow, _classNamePtr, _trayIcon;
    private static Native.NOTIFYICONDATA _tray;
    private static bool _trayAdded, _exiting, _active;
    private static long _lastRender;
    private static IDesktopOverlay? _surface;
    private static bool _useFallback;
    private static float _aimX, _aimY;
    private static long _profileStart;
    private static bool _openSettings, _startActive;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
        {
            Environment.ExitCode = SelfTest.Run();
            return;
        }
        if(args.Length==2&&args[0]=="--export-icon"){LampIcon.Write(args[1]);return;}
        if(args.Length==2&&args[0]=="--ui-preview"){SettingsWindow.WritePreview(args[1]);return;}
        _openSettings = Array.IndexOf(args, "--settings") >= 0;
        _startActive = Array.IndexOf(args, "--active") >= 0;
        if (args.Length == 2 && args[0] == "--benchmark")
        {
            Environment.ExitCode = SelfTest.Benchmark(args[1]);
            return;
        }
        if (args.Length >= 3 && args[0] == "--profile")
        {
            if(args[1]!="cpu"){Environment.ExitCode=2;return;}
            FrameMetrics.Output = args[2];
            LightSettings.Reset();
            FrameMetrics.Idle = Array.IndexOf(args, "--idle") >= 0;
            if (args.Length >= 5)
            {
                FrameMetrics.TestWidth = int.Parse(args[3]);
                FrameMetrics.TestHeight = int.Parse(args[4]);
            }
        }
        if (!OperatingSystem.IsWindows()) return;

        using Mutex singleInstance = new(true, "Local\\Spotlighter.6D5C334E", out bool firstInstance);
        if (!firstInstance)
        {
            if (FrameMetrics.Output != null || Array.IndexOf(args, "--integration-test") >= 0)
                Environment.ExitCode = 2;
            return;
        }
        try
        {
            Initialize();
            if (args.Length == 2 && args[0] == "--integration-test")
            { Environment.ExitCode = IntegrationTest.Run(WindowClass, _module, args[1]); return; }
            if (FrameMetrics.Output != null) { Toggle(); FrameMetrics.Start(); _profileStart = Environment.TickCount64; }
            if (_startActive && !_active) Toggle();
            if (_openSettings) OpenSettings();
            using FrameClock clock = new();
            clock.Run(Tick, () => _active);
        }
        catch (Exception e)
        {
            Environment.ExitCode=1;
            Native.MessageBoxW(0, e.Message, "Spotlighter could not start", 0x10);
        }
        finally
        {
            Shutdown();
        }
    }

    private static void Initialize()
    {
        // Negative four is DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2.
        Native.SetProcessDpiAwarenessContext(new nint(-4));
        _module = Native.GetModuleHandleW(null);
        _classNamePtr = Marshal.StringToHGlobalUni(WindowClass);
        Native.WNDCLASSEX cls = new()
        {
            cbSize = (uint)sizeof(Native.WNDCLASSEX),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowCallback),
            hInstance = _module,
            lpszClassName = _classNamePtr
        };
        if (Native.RegisterClassExW(ref cls) == 0)
            throw new InvalidOperationException("Could not register native window class.");

        _messageWindow = Native.CreateWindowExW(0, WindowClass, "Spotlighter", 0,
            0, 0, 0, 0, 0, 0, _module, 0);
        if (_messageWindow == 0)
            throw new InvalidOperationException("Could not create the tray message window.");

        InstallTray();
        LampOverlay.ToggleLight=Toggle;
        EnsureLamp();
        LampOverlay.OpenControls=(x,y)=>{if(!_active)Toggle();SettingsWindow.Toggle(x,y);};
        SettingsWindow.Changed = () => _drawX=float.NaN;
        SettingsWindow.Status = () => _surface is CompositionOverlay ? "缓存光照" : "兼容模式";

    }

    private static void EnsureLamp()
    {
        if(_lamp!=null)return;
        _lamp=new LampOverlay(_module);
        Native.GetCursorPos(out Native.POINT p);_lamp.Render(p.X,p.Y);_lamp.SetLit(_active);_lamp.Show();
    }

    private static void InstallTray()
    {
        _trayIcon=LampIcon.Create();
        Native.NOTIFYICONDATA data = new()
        {
            cbSize = (uint)sizeof(Native.NOTIFYICONDATA),
            hWnd = _messageWindow,
            uID = 1,
            uFlags = Native.NIF_ICON | Native.NIF_MESSAGE | Native.NIF_TIP,
            uCallbackMessage = TrayMessage,
            hIcon = _trayIcon
        };
        const string tip = "Spotlighter · 台灯单击开关、双击调光";
        for (int i = 0; i < tip.Length && i < 127; i++) data.szTip[i] = tip[i];
        _tray = data;
        _trayAdded = Native.Shell_NotifyIconW(Native.NIM_ADD, ref _tray);
    }

    private static nint WindowProc(nint hwnd, uint msg, nuint wp, nint lp)
    {
        // Composition windows have no alpha hit-test. The extended
        // transparent style plus this response gives Windows a second
        // opportunity to pass clicks to the underlying application.
        if (hwnd != _messageWindow && msg == 0x0084) return new nint(-1);
        if (hwnd != _messageWindow && msg == 0x0021) return new nint(3);
        if (hwnd == _messageWindow)
        {
            if (msg == Native.WM_TIMER)
            {
                Tick();
                return 0;
            }
            if (msg == TrayMessage)
            {
                if ((uint)lp == Native.WM_RBUTTONUP) ShowTrayMenu();
                else if ((uint)lp == Native.WM_LBUTTONDBLCLK) Toggle();
                return 0;
            }
            if (msg == Native.WM_DISPLAYCHANGE)
            {
                RecreateDesktop();
                return 0;
            }
            if (msg == Native.WM_CLOSE)
            {
                Shutdown();
                return 0;
            }
            if (msg == Native.WM_DESTROY)
            {
                Native.PostQuitMessage(0);
                return 0;
            }
        }
        return Native.DefWindowProcW(hwnd, msg, wp, lp);
    }

    private static void Tick()
    {
        if (_exiting) return;
        long now = Environment.TickCount64;
        if (FrameMetrics.Output != null && now - _profileStart > 15000 && _surface != null)
        { FrameMetrics.Save(_surface); Shutdown(); return; }
        if (!_active || _surface is null) return;

        float dt = _lastRender == 0 ? 1f / 60 : Math.Clamp((now - _lastRender) / 1000f, .001f, .25f);
        _lastRender = now;
        try
        {
            if (!FrameMetrics.Idle && Native.GetCursorPos(out Native.POINT position)
                && !SettingsWindow.ContainsPointer(position.X,position.Y) && _lamp?.ContainsPointer(position.X,position.Y)!=true)
            {
                float easing = 1 - MathF.Exp(-dt / .022f);
                _aimX += (position.X - _aimX) * easing;
                _aimY += (position.Y - _aimY) * easing;
            }
            if (FrameMetrics.Output != null && !FrameMetrics.Idle)
            {
                _aimX = _surface.Left + _surface.Width * (.5f + .3f * MathF.Sin(now * .002f));
                _aimY = _surface.Top + _surface.Height * (.5f + .3f * MathF.Cos(now * .0013f));
            }
            nint foreground=Native.GetForegroundWindow();
            if(foreground!=_foreground)
            {
                _foreground=foreground;
                if(_surface is DesktopSurface cpu)cpu.Raise();
                if(_surface is CompositionOverlay composed)composed.Raise();
                _lamp?.Show();
                if(SettingsWindow.Handle!=0) Native.SetWindowPos(SettingsWindow.Handle,Native.HWND_TOPMOST,0,0,0,0,0x13);
            }
            if((FrameMetrics.Output==null || FrameMetrics.Idle) && MathF.Abs(_aimX-_drawX)<.25f && MathF.Abs(_aimY-_drawY)<.25f && _drawSettings==LightSettings.Current) return;
            bool changed=!float.IsFinite(_drawX);
            _drawX=_aimX;_drawY=_aimY;_drawSettings=LightSettings.Current;
            long preciseStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _surface.Render(_aimX, _aimY);
            _lamp?.Render(_aimX,_aimY);
            if(changed) { _lamp?.Show(); if(SettingsWindow.Handle!=0)Native.SetWindowPos(SettingsWindow.Handle,Native.HWND_TOPMOST,0,0,0,0,0x13); }
            FrameMetrics.Record(FrameMetrics.Milliseconds(preciseStart));
        }
        catch (Exception e)
        {
            bool compositionFailed = _surface is CompositionOverlay;
            _active = false;
            _lamp?.Dispose();_lamp=null;
            _surface?.Dispose();
            _surface = null;
            if (compositionFailed)
            {
                // Recover from compositor or device failures.
                ReportCompositionFailure(e);
                _useFallback = true;
                Toggle();
            }
            else Native.MessageBoxW(0, e.Message, "Spotlighter rendering failed", 0x30);
        }
    }

    private static void Toggle()
    {
        if (_exiting) return;
        try
        {
            if (_active)
            {
                _active = false;
                SettingsWindow.Close();
                _surface?.Deactivate();
                _lamp?.SetLit(false);_lamp?.Show();
                return;
            }
            if (_surface is null)
            {
                if(!_useFallback)
                {
                    try { _surface=new CompositionOverlay(WindowClass,_module); }
                    catch(Exception e) { ReportCompositionFailure(e);_useFallback=true; }
                }
                _surface ??= new DesktopSurface(WindowClass,_module);

            }
            EnsureLamp();
            if (Native.GetCursorPos(out Native.POINT pt))
            {
                _aimX = pt.X;
                _aimY = pt.Y;
            }
            else
            {
                _aimX = _surface.Left + _surface.Width * 0.5f;
                _aimY = _surface.Top + _surface.Height * 0.6f;
            }
            _surface.Activate();
            _lamp?.Render(_aimX,_aimY);_lamp?.SetLit(true);_lamp?.Show();
            _drawX=float.NaN;
            _lastRender = 0;
            _active = true;
        }
        catch (Exception e)
        {
            _active = false;
            _lamp?.Dispose();_lamp=null;
            _surface?.Dispose();
            _surface = null;
            Native.MessageBoxW(0, e.Message, "Spotlighter cannot activate", 0x30);
        }
    }

    private static void RecreateDesktop()
    {
        SettingsWindow.Close();
        bool wasActive = _active;
        _useFallback=false;
        _active = false;
        _lamp?.Dispose();_lamp=null;
        _surface?.Dispose();
        _surface = null;
        if (wasActive) Toggle();
        else EnsureLamp();
    }

    private static void ShowTrayMenu()
    {
        if(FrameMetrics.Output!=null)FrameMetrics.Interrupted=true;
        nint menu = Native.CreatePopupMenu();
        if (menu == 0) return;
        try
        {
            Native.AppendMenuW(menu, Native.MF_STRING, 1, _active ? "关闭灯光" : "开启灯光");
            Native.AppendMenuW(menu, Native.MF_STRING, 4, "设置…");
            Native.AppendMenuW(menu, Native.MF_STRING, 2, "退出");
            Native.GetCursorPos(out Native.POINT cursor);
            Native.SetForegroundWindow(_messageWindow);
            uint choice = Native.TrackPopupMenu(menu, Native.TPM_RIGHTBUTTON | Native.TPM_RETURNCMD,
                cursor.X, cursor.Y, 0, _messageWindow, 0);
            if (choice == 4) OpenSettings();
            else if (choice == 1) Toggle();
            else if (choice == 2) Shutdown();
        }
        finally { Native.DestroyMenu(menu); }
    }

    private static void OpenSettings()
    {
        if (!_active) Toggle();
        if(_lamp!=null)SettingsWindow.Show(_lamp.PanelX,_lamp.PanelY);
        else SettingsWindow.Show();
    }

    private static void ReportCompositionFailure(Exception ex)
    {
        // Composition failures fall back to the retained Win32 bitmap. Keep a diagnostic
        // without blocking the user behind an activation failure dialog.
        try
        {
            string folder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Spotlighter");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "composition-fallback.log"),
                DateTimeOffset.Now + Environment.NewLine + ex);
        }
        catch { }
    }

    private static void Shutdown()
    {
        if (_exiting) return;
        _exiting = true;
        SettingsWindow.Close();
        _active = false;
        _lamp?.Dispose();_lamp=null;
        _surface?.Dispose();
        _surface = null;
        if (_trayAdded) { Native.Shell_NotifyIconW(Native.NIM_DELETE, ref _tray); _trayAdded = false; }
        if (_messageWindow != 0)
        {
            nint hwnd = _messageWindow;
            _messageWindow = 0;
            Native.DestroyWindow(hwnd);
        }
        if(_trayIcon!=0){LampIcon.DestroyIcon(_trayIcon);_trayIcon=0;}
        Native.PostQuitMessage(0);
        if (_classNamePtr != 0) { Marshal.FreeHGlobal(_classNamePtr); _classNamePtr = 0; }
    }
}

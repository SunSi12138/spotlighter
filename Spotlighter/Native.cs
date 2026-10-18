using System;
using System.Runtime.InteropServices;

namespace Spotlighter;

internal static unsafe class Native
{
    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_EX_TOPMOST = 0x00000008;
    internal const uint WS_EX_TRANSPARENT = 0x00000020;
    internal const uint WS_EX_TOOLWINDOW = 0x00000080;
    internal const uint WS_EX_LAYERED = 0x00080000;
    internal const uint WS_EX_NOACTIVATE = 0x08000000;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNA = 8;
    internal const uint WM_DESTROY = 0x0002;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint WM_DISPLAYCHANGE = 0x007E;
    internal const uint WM_TIMER = 0x0113;
    internal const uint WM_RBUTTONUP = 0x0205;
    internal const uint WM_LBUTTONDBLCLK = 0x0203;
    internal const uint WM_USER = 0x0400;
    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;
    internal const uint ULW_ALPHA = 0x00000002;
    internal const uint NIM_ADD = 0;
    internal const uint NIM_DELETE = 2;
    internal const uint NIF_MESSAGE = 1;
    internal const uint NIF_ICON = 2;
    internal const uint NIF_TIP = 4;
    internal const uint TPM_RIGHTBUTTON = 0x0002;
    internal const uint TPM_RETURNCMD = 0x0100;
    internal const uint MF_STRING = 0;
    internal static readonly nint HWND_TOPMOST = new(-1);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEX
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        public nint lpszMenuName, lpszClassName, hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X, Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int cx, cy;
        public SIZE(int width, int height) { cx = width; cy = height; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState, dwStateMask;
        public fixed char szInfo[256];
        public uint uTimeoutOrVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern nint GetModuleHandleW(string? name);

    [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(nint context);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, ExactSpelling = true)]
    internal static extern ushort RegisterClassExW(ref WNDCLASSEX wc);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    internal static extern nint CreateWindowExW(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", ExactSpelling = true)]
    internal static extern nint DefWindowProcW(nint hwnd, uint msg, nuint wp, nint lp);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "ShowWindow", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "TranslateMessage", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW", ExactSpelling = true)]
    internal static extern nint DispatchMessageW(ref MSG msg);

    [DllImport("user32.dll", EntryPoint = "PostQuitMessage", ExactSpelling = true)]
    internal static extern void PostQuitMessage(int code);

    [DllImport("user32.dll", EntryPoint = "SetTimer", ExactSpelling = true)]
    internal static extern nuint SetTimer(nint hwnd, nuint timerId, uint intervalMs, nint callback);

    [DllImport("user32.dll", EntryPoint = "KillTimer", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool KillTimer(nint hwnd, nuint timerId);

    [DllImport("user32.dll", EntryPoint = "GetSystemMetrics", ExactSpelling = true)]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", EntryPoint = "GetCursorPos", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateLayeredWindow(nint hwnd, nint dstDC, ref POINT dst, ref SIZE size,
        nint sourceDC, ref POINT source, uint colorKey, ref BLENDFUNCTION blend, uint flags);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC", ExactSpelling = true)]
    internal static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll", EntryPoint = "DeleteDC", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(nint dc);

    [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", ExactSpelling = true)]
    internal static extern nint CreateDIBSection(nint dc, ref BITMAPINFO info, uint usage,
        out nint bits, nint section, uint offset);

    [DllImport("gdi32.dll", EntryPoint = "SelectObject", ExactSpelling = true)]
    internal static extern nint SelectObject(nint dc, nint obj);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(nint obj);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern nint LoadCursorW(nint module, nint name);

    [DllImport("user32.dll", EntryPoint = "CreatePopupMenu", ExactSpelling = true)]
    internal static extern nint CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenuW(nint menu, uint flags, nuint id, string text);

    [DllImport("user32.dll", EntryPoint = "TrackPopupMenu", ExactSpelling = true)]
    internal static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint owner, nint rectangle);

    [DllImport("user32.dll", EntryPoint = "DestroyMenu", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int MessageBoxW(nint owner, string message, string title, uint flags);
}

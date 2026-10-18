using System;
using System.Runtime.InteropServices;

namespace Spotlighter;

internal readonly struct WelcomeLayout
{
    internal const int DesignWidth=680,DesignHeight=600;
    internal readonly int Left,Top,Width,Height;
    internal readonly float Scale;
    internal WelcomeLayout(int left,int top,int width,int height,uint dpi)
    {
        float fit=MathF.Min(Math.Max(1,width-48)/(float)DesignWidth,Math.Max(1,height-48)/(float)DesignHeight);
        Scale=Math.Max(.1f,MathF.Min(Math.Max(96,dpi)/96f,fit));
        Width=Math.Max(1,(int)MathF.Round(DesignWidth*Scale));Height=Math.Max(1,(int)MathF.Round(DesignHeight*Scale));
        Left=left+(width-Width)/2;Top=top+(height-Height)/2;
    }
}

// One native dimming window + one small retained help card. No full-desktop
// CPU bitmap, capture, timer or animation. All resources are freed on dismissal.
internal static unsafe class WelcomeOverlay
{
    private const string BackdropClass="Spotlighter.WelcomeBackdrop",PanelClass="Spotlighter.WelcomePanel";
    private static readonly Native.WndProc Callback=Proc;
    private static bool _registered,_preview;
    private static nint _backdrop;
    private static OverlayBitmap? _panel;
    private static WelcomeLayout _layout;
    internal static Action? Closed;
    internal static bool Visible=>_panel!=null;
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { internal uint Size;internal Rect Monitor,Work;internal uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor,int kind,out uint x,out uint y);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int index);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);

    private static void Register(nint module)
    {
        if(_registered)return;
        foreach(string name in new[]{BackdropClass,PanelClass})
        {
            nint text=Marshal.StringToHGlobalUni(name);
            try
            {
                Native.WNDCLASSEX cls=new(){cbSize=(uint)sizeof(Native.WNDCLASSEX),lpfnWndProc=Marshal.GetFunctionPointerForDelegate(Callback),hInstance=module,lpszClassName=text,hCursor=Native.LoadCursorW(0,32512),hbrBackground=name==BackdropClass?GetStockObject(4):0};
                if(Native.RegisterClassExW(ref cls)==0)throw new InvalidOperationException("Cannot register welcome window");
            }
            finally{Marshal.FreeHGlobal(text);}
        }
        _registered=true;
    }
    internal static void Show()
    {
        if(_panel!=null){_panel.Show();Native.SetForegroundWindow(_panel.Window);return;}
        nint module=Native.GetModuleHandleW(null);Register(module);
        nint monitor=MonitorFromWindow(0,1);MonitorInfo info=new(){Size=(uint)sizeof(MonitorInfo)};
        if(!GetMonitorInfoW(monitor,ref info))info.Monitor=new(){L=0,T=0,R=Native.GetSystemMetrics(0),B=Native.GetSystemMetrics(1)};
        uint dpi=96;if(GetDpiForMonitor(monitor,0,out uint actualDpi,out _)>=0)dpi=actualDpi;
        _layout=_preview?new(0,0,1920,1080,96):new(info.Monitor.L,info.Monitor.T,info.Monitor.R-info.Monitor.L,info.Monitor.B-info.Monitor.T,dpi);
        try
        {
            if(!_preview)
            {
                _backdrop=Native.CreateWindowExW(Native.WS_EX_LAYERED|Native.WS_EX_TOPMOST|Native.WS_EX_TOOLWINDOW|Native.WS_EX_NOACTIVATE,BackdropClass,"Spotlighter help backdrop",Native.WS_POPUP,Native.GetSystemMetrics(76),Native.GetSystemMetrics(77),Native.GetSystemMetrics(78),Native.GetSystemMetrics(79),0,0,module,0);
                if(_backdrop==0||!Native.SetLayeredWindowAttributes(_backdrop,0,195,2))throw new InvalidOperationException("Cannot show welcome backdrop");
                Native.ShowWindow(_backdrop,Native.SW_SHOWNA);
            }
            _panel=new OverlayBitmap(PanelClass,module,_layout.Width,_layout.Height,true,true,"Spotlighter · 操作说明");
            Paint();_panel.Upload(_layout.Left,_layout.Top);
            if(!_preview){_panel.Show();Native.SetForegroundWindow(_panel.Window);SetFocus(_panel.Window);}
        }
        catch{Close();throw;}
    }
    private static void Paint()
    {
        using UiCanvas c=new(_panel!,_layout.Scale);
        c.Round(1,1,678,598,26,0x60ffffff);c.Round(2,2,676,596,25,0xfb1b1e23);
        c.Circle(35,36,4,0xffe4b975);c.Text("SPOTLIGHTER",49,25,400,13,0xffe4b975,true);
        c.Text("×",636,18,30,21,0xffbcbec1);
        c.Text("桌面上的一盏小台灯",28,65,624,30,0xfff6eddf,true);
        c.Text("先花几秒，认识一下这三个操作。",30,111,620,13,0xffaaaeb7);
        Row(c,150,"1","单击小台灯，开灯或关灯","灯在主屏顶部，关灯后也会保留。");
        Row(c,240,"2","双击小台灯，打开调光面板","范围、亮度、柔和度、环境光与冷暖，都能即时预览。");
        Row(c,330,"3","右键托盘图标，随时退出","双击托盘也能开关灯光；设置会自动保存。");
        c.Text("灯光跟随鼠标，桌面仍能正常点击、输入和滚动。",30,429,624,13,0xffd5d7db);
        c.Text("开始菜单、通知中心等系统浮层暂不覆盖。",30,461,624,12,0xff969ba5);
        c.Round(28,500,624,48,13,0xffe4bb7c);c.Text("知道了，开始使用",273,512,350,16,0xff27231c,true);
        c.Text("按 Esc 收起 · 以后可从托盘菜单查看「操作说明」",171,565,500,11,0xff969ba5);
    }
    private static void Row(UiCanvas c,float y,string number,string title,string text)
    {
        c.Round(28,y,624,80,14,0x0cffffff);c.Circle(58,y+40,18,0x24e4b975);
        c.Text(number,53,y+28,30,14,0xffe4b975,true);
        c.Text(title,92,y+12,540,17,0xffece8df,true);c.Text(text,92,y+44,540,12,0xffadb1b9);
    }
    private static void Finish()
    {
        if(!_preview)WelcomeState.MarkSeen(WelcomeState.FilePath);
        Close(true);
    }
    internal static void Close(bool notify=false)
    {
        OverlayBitmap? panel=_panel;_panel=null;panel?.Dispose();
        nint backdrop=_backdrop;_backdrop=0;if(backdrop!=0)Native.DestroyWindow(backdrop);
        if(notify)Closed?.Invoke();
    }
    internal static void WritePreview(string path)
    {
        _preview=true;
        try{Show();UiCanvas.SavePng(_panel!,path);}
        finally{Close();_preview=false;}
    }
    private static nint Proc(nint window,uint message,nuint wp,nint lp)
    {
        try
        {
            if(message==0x84)return 1;
            if(message==0x21)return window==_backdrop?3:1;
            if(message==0x100&&(wp==27||wp==13||wp==32)){Finish();return 0;}
            if(message==0x10){Finish();return 0;}
            if(message==0x202&&window==_panel?.Window)
            {
                float x=(short)((long)lp&65535)/_layout.Scale,y=(short)(((long)lp>>16)&65535)/_layout.Scale;
                if((x>=28&&x<=652&&y>=500&&y<=548)||(x>=624&&y<=57))Finish();
                return 0;
            }
            if(message==0x201&&window==_backdrop&&_panel!=null){SetFocus(_panel.Window);return 0;}
        }
        catch(Exception e){Close(true);Native.MessageBoxW(0,e.Message,"Spotlighter",0x30);}
        return Native.DefWindowProcW(window,message,wp,lp);
    }
}

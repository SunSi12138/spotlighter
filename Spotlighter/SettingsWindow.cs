using System;
using System.Runtime.InteropServices;
namespace Spotlighter;

// One small premultiplied-alpha popup; no native trackbar or opaque form.
internal static unsafe class SettingsWindow
{
    private const int Width=360,Height=414;
    private static readonly Native.WndProc Callback=Proc;
    private static readonly string[] Names={"光源范围","照明亮度","边缘柔和度","环境亮度","光色冷暖"};
    private static readonly float[] Min={.35f,.2f,0,0,-1},Max={2.5f,1.8f,2.5f,.3f,1};
    // Ambient steps are fractions of slider travel; other steps are physical values.
    private static readonly float[] Step={.02f,.02f,.025f,.01f,.02f};
    private static OverlayBitmap? _image;
    private static bool _registered,_dirty,_paintPending,_preview;
    private static int _left,_top,_selected,_drag=-1;
    private static float _scale=1;
    internal static Action? Changed;
    internal static Func<string>? Status;
    internal static nint Handle=>_image?.Window??0;
    internal static bool Visible=>_image!=null;
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size;public Rect Monitor,Work;public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Native.POINT p,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor,int kind,out uint x,out uint y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out Rect rect);
    [DllImport("user32.dll")] private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint window,uint msg,nuint wp,nint lp);
    internal static bool ContainsPointer(int x,int y)=>_image!=null && (_drag>=0 || (x>=_left&&y>=_top&&x<_left+_image.Width&&y<_top+_image.Height));
    internal static void Toggle(int x,int y){if(Visible)Close();else Show(x,y);}
    internal static void WritePreview(string path) { _preview=true;try{Show(0,0);UiCanvas.SavePng(_image!,path);}finally{Close();_preview=false;} }
    internal static void Show()
    {
        Native.GetCursorPos(out Native.POINT p);Show(p.X+18,p.Y+18);
    }
    internal static void Show(int x,int y)
    {
        if(_image!=null){_image.Show();Native.SetForegroundWindow(Handle);return;}
        nint module=Native.GetModuleHandleW(null);
        if(!_registered)
        {
            nint name=Marshal.StringToHGlobalUni("Spotlighter.FloatingControls");
            try{
                Native.WNDCLASSEX cls=new(){cbSize=(uint)sizeof(Native.WNDCLASSEX),lpfnWndProc=Marshal.GetFunctionPointerForDelegate(Callback),hInstance=module,lpszClassName=name,hCursor=Native.LoadCursorW(0,32512)};
                if(Native.RegisterClassExW(ref cls)==0)throw new InvalidOperationException("Cannot register floating controls");
                _registered=true;
            }finally{Marshal.FreeHGlobal(name);}
        }
        nint monitor=MonitorFromPoint(new Native.POINT(x,y),2);
        _scale=GetDpiForMonitor(monitor,0,out uint dpi,out _)>=0?dpi/96f:1;
        _image=new OverlayBitmap("Spotlighter.FloatingControls",module,Px(Width),Px(Height),true,true,"Spotlighter · 调光");
        _left=x;_top=y;ClampToMonitor(monitor);_drag=-1;_selected=0;
        Render();if(!_preview){_image.Show();Native.SetForegroundWindow(Handle);SetFocus(Handle);}
    }
    private static int Px(float value)=>(int)MathF.Round(value*_scale);
    private static void ClampToMonitor(nint monitor)
    {
        MonitorInfo info=new(){Size=(uint)sizeof(MonitorInfo)};
        if(GetMonitorInfoW(monitor,ref info)&&_image!=null)
        {
            _left=Math.Clamp(_left,info.Work.L,Math.Max(info.Work.L,info.Work.R-_image.Width));
            _top=Math.Clamp(_top,info.Work.T,Math.Max(info.Work.T,info.Work.B-_image.Height));
        }
    }
    private static float Get(int i)
    {
        LightSettings s=LightSettings.Current;
        return i switch{0=>s.Size,1=>s.Brightness,2=>s.Softness,3=>s.Ambient,_=>s.Temperature};
    }
    internal static float ToSlider(int i,float value)
    {
        float position=Math.Clamp((value-Min[i])/(Max[i]-Min[i]),0,1);
        return i==3?MathF.Cbrt(position):position;
    }
    internal static float FromSlider(int i,float position)
    {
        position=Math.Clamp(position,0,1);
        if(i==3)position=position*position*position;
        return Min[i]+position*(Max[i]-Min[i]);
    }
    internal static float AdjustedValue(int i,float value,float steps)=>i==3
        ?FromSlider(i,ToSlider(i,value)+steps*Step[i])
        :Math.Clamp(value+steps*Step[i],Min[i],Max[i]);
    private static string Value(int i)=>i switch{0=>$"{Get(i):0.00}×",1=>$"{Get(i)*100:0}%",2=>$"{Get(i):0.00}",3=>$"{Get(i):0.0000}",_=>MathF.Abs(Get(i))<.005f?"0.00":$"{Get(i):+0.00;-0.00}"};
    private static void Set(int i,float value)
    {
        value=Math.Clamp(value,Min[i],Max[i]);
        if(i==3?Get(i)==value:MathF.Abs(Get(i)-value)<.00001f)return;
        LightSettings s=LightSettings.Current;
        switch(i){case 0:s.Size=value;break;case 1:s.Brightness=value;break;case 2:s.Softness=value;break;case 3:s.Ambient=value;break;case 4:s.Temperature=value;break;}
        s.Clamp();_dirty=true;Changed?.Invoke();Native.SetTimer(Handle,2,350,0);RequestPaint();
    }
    private static void RequestPaint(){if(Handle!=0&&!_paintPending){_paintPending=true;Native.SetTimer(Handle,3,16,0);}}
    private static void Render()
    {
        if(_image==null)return;
        using(UiCanvas c=new(_image,_scale))
        {
            c.Round(1,1,358,412,22,0x40000000);
            c.Round(2,2,356,410,21,0x38ffffff);
            c.Round(3,3,354,408,20,_drag>=0?0xae17191d:0xd91b1e23);
            c.Circle(28,34,4,0xffe4b975);
            c.Text("Spotlighter",42,19,248,23,0xfff6eddf,true);
            c.Text("×",320,15,30,23,0xffc4c4c0);
            for(int i=0;i<5;i++)
            {
                float y=66+i*58,track=y+35;
                c.Text(Names[i],24,y,190,14,0xffdedbd4);
                c.Text(Value(i),254,y,88,14,0xffe8bf83);
                c.Round(28,track,304,5,2.5f,0x55b0b4bc);
                float normalized=ToSlider(i,Get(i)),x=28+normalized*304;
                if(i==4)
                {
                    c.Round(179,track-3,2,11,1,0x80ffffff);
                    if(MathF.Abs(x-180)>1)c.Round(Math.Min(x,180),track,Math.Max(5,MathF.Abs(x-180)),5,2.5f,Get(i)<0?0xff8bb3df:0xffc99e62);
                }
                else if(x>29)c.Round(28,track,Math.Max(5,x-28),5,2.5f,0xffc99e62);
                if(i==_selected)c.Circle(x,track+2.5f,12,0x35f0c68a);
                c.Circle(x,track+2.5f,7,i==4&&Get(i)<0?0xffb8d5f2:0xfff1d1a0);
                c.Circle(x-1,track+1.5f,2,0xfffff0d7);
            }
            c.Text("冷",24,343,32,10,0xffa9c9e9);
            c.Text("原色",165,343,50,10,0xffaaa9a4);
            c.Text("暖",319,343,32,10,0xffd7b57f);
            c.Round(24,368,138,30,10,0x26ffffff);
            c.Text("恢复默认",62,372,100,13,0xffddd6ca);
            c.Round(232,368,104,30,10,0x35d7ad71);
            c.Text("完成",269,372,60,13,0xfff1ddbf);
            if(LightSettings.SaveError!=null)c.Text("设置保存失败",24,351,300,11,0xffffad8e);
        }
        _image.Upload(_left,_top);
    }
    private static int Row(float y){int i=(int)MathF.Floor((y-61)/58);return i>=0&&i<5&&y<=61+i*58+55?i:-1;}
    private static void Drag(float x)
    {
        if(_drag<0)return;
        float value=FromSlider(_drag,(x-28)/304);
        Set(_drag,value);
    }
    private static void Save(){if(_dirty){LightSettings.Save();_dirty=false;}if(Handle!=0)Native.KillTimer(Handle,2);}
    internal static void Close()
    {
        if(_image==null)return;
        Save();OverlayBitmap image=_image;_image=null;_drag=-1;_paintPending=false;
        Native.KillTimer(image.Window,3);ReleaseCapture();image.Dispose();
    }
    private static nint Proc(nint hwnd,uint msg,nuint wp,nint lp)
    {
        try{
            float x=(short)((long)lp&0xffff)/_scale,y=(short)(((long)lp>>16)&0xffff)/_scale;
            if(msg==0x84)return 1;
            if(msg==0x201)
            {
                SetFocus(hwnd);
                if(x>312&&y<55){Close();return 0;}
                if(y>=368&&y<=402){if(x<175){LightSettings.Reset();LightSettings.Save();Changed?.Invoke();RequestPaint();}else if(x>224)Close();return 0;}
                int row=Row(y);
                if(row>=0){_selected=_drag=row;SetCapture(hwnd);Drag(x);RequestPaint();return 0;}
                if(y<56){ReleaseCapture();SendMessageW(hwnd,0xa1,2,0);return 0;}
            }
            if(msg==0x200&&_drag>=0){Drag(x);return 0;}
            if(msg==0x202||msg==0x215){if(_drag>=0){_drag=-1;ReleaseCapture();Save();RequestPaint();}return 0;}
            if(msg==0x20a){Set(_selected,AdjustedValue(_selected,Get(_selected),(short)((wp>>16)&0xffff)/120f));return 0;}
            if(msg==0x100)
            {
                int key=(int)wp;
                if(key==27){Close();return 0;}
                if(key==9){_selected=(_selected+(GetKeyState(16)<0?4:1))%5;RequestPaint();return 0;}
                if(key==38||key==40){_selected=(_selected+(key==38?4:1))%5;RequestPaint();return 0;}
                if(key==37||key==39){Set(_selected,AdjustedValue(_selected,Get(_selected),key==37?-1:1));return 0;}
                if(key==36||key==35){Set(_selected,key==36?Min[_selected]:Max[_selected]);return 0;}
                if(key==13){Close();return 0;}
            }
            if(msg==0x113)
            {
                if(wp==2)Save();
                if(wp==3){Native.KillTimer(hwnd,3);_paintPending=false;Render();}
                return 0;
            }
            if(msg==3&&GetWindowRect(hwnd,out Rect r)){_left=r.L;_top=r.T;}
            if(msg==Native.WM_CLOSE){Close();return 0;}
        }catch(Exception e){Native.MessageBoxW(hwnd,e.Message,"Spotlighter",0x30);}
        return Native.DefWindowProcW(hwnd,msg,wp,lp);
    }
}

using System;
using System.Runtime.InteropServices;
namespace Spotlighter;

internal sealed unsafe class LampOverlay : IDisposable
{
    private const int W=240,H=190,Count=48;
    private static readonly Native.WndProc Callback=Proc;
    private static bool _registered;
    private static LampOverlay? _current;
    internal static Action<int,int>? OpenControls;
    internal static Action? ToggleLight;
    private readonly OverlayBitmap _image;
    private readonly LampClickGesture _click=new();
    private readonly uint[] _frames=new uint[W*H*Count];
    private readonly uint[] _offFrame=new uint[W*H];
    private readonly float[] _frameTemperatures=new float[Count];
    private float _drawTemperature=float.NaN;
    private int _angle=-1;
    private readonly LampLayout _layout;
    private bool _visible,_lit=true;
    private float _aimX,_aimY;
    internal int PanelX=>_layout.PanelX;
    internal int PanelY=>_layout.PanelY;
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    internal LampOverlay(nint module)
    {
        Register(module);
        _layout=LampLayout.Primary();
        _image=new OverlayBitmap("Spotlighter.LampControl",module,_layout.Width,_layout.Height,true);
        _current=this;
        Array.Fill(_frameTemperatures,float.NaN);
    }
    private static void Register(nint module)
    {
        if(_registered)return;
        nint name=Marshal.StringToHGlobalUni("Spotlighter.LampControl");
        try{
            Native.WNDCLASSEX cls=new(){cbSize=(uint)sizeof(Native.WNDCLASSEX),style=8,lpfnWndProc=Marshal.GetFunctionPointerForDelegate(Callback),hInstance=module,lpszClassName=name,hCursor=Native.LoadCursorW(0,32649)};
            if(Native.RegisterClassExW(ref cls)==0)throw new InvalidOperationException("Cannot register lamp control");
            _registered=true;
        }finally{Marshal.FreeHGlobal(name);}
    }
    internal bool ContainsPointer(int x,int y)
    {
        x-=_layout.Left;y-=_layout.Top;
        return _visible&&x>=0&&y>=0&&x<_image.Width&&y<_image.Height&&(_image.Pixels[y*_image.Width+x]>>24)>=64;
    }
    private static nint Proc(nint window,uint msg,nuint wp,nint lp)
    {
        try{
            if(msg==0x84)
            {
                int x=(short)((long)lp&0xffff),y=(short)(((long)lp>>16)&0xffff);
                return _current?.ContainsPointer(x,y)==true?1:-1;
            }
            if(msg==0x21)return 3;
            if(_current is LampOverlay lamp)
            {
                if(msg==0x201){lamp._click.Press();SetCapture(window);return 0;}
                if(msg==0x202)
                {
                    bool single=lamp._click.Release();ReleaseCapture();
                    Native.GetCursorPos(out Native.POINT p);
                    if(single&&lamp.ContainsPointer(p.X,p.Y))Native.SetTimer(window,1,GetDoubleClickTime()+10,0);
                    return 0;
                }
                if(msg==0x203)
                {
                    Native.KillTimer(window,1);lamp._click.DoubleClick();SetCapture(window);
                    OpenControls?.Invoke(lamp.PanelX,lamp.PanelY);return 0;
                }
                if(msg==0x113&&wp==1)
                {
                    Native.KillTimer(window,1);
                    if(lamp._click.FireSingle())ToggleLight?.Invoke();
                    return 0;
                }
            }
        }catch(Exception e){Native.MessageBoxW(window,e.Message,"Spotlighter",0x30);}
        return Native.DefWindowProcW(window,msg,wp,lp);
    }
    internal void SetLit(bool lit)
    {
        if(_lit==lit)return;
        _lit=lit;_angle=-1;Render(_aimX,_aimY);
    }
    internal void Render(float x,float y)
    {
        _aimX=x;_aimY=y;
        float a=MathF.Atan2(y-_layout.PivotY,x-_layout.CenterX);
        int frame=((int)MathF.Round(a*Count/MathF.Tau)+Count)%Count;
        float temperature=_lit?LightSettings.Current.Temperature:0;
        if(frame==_angle&&_drawTemperature==temperature)return;
        _angle=frame;_drawTemperature=temperature;
        if(_lit)
        {
            if(_frameTemperatures[frame]!=temperature)
            {
                Span<uint> pixels=_frames.AsSpan(frame*W*H,W*H);pixels.Clear();
                float angle=frame*MathF.Tau/Count;
                fixed(uint* p=pixels)LampPainter.Draw(p,W,H,W/2+MathF.Cos(angle)*1000,78+MathF.Sin(angle)*1000,true,temperature);
                _frameTemperatures[frame]=temperature;
            }
            LampRaster.Copy(_frames.AsSpan(frame*W*H,W*H),new Span<uint>(_image.Pixels,_image.Width*_image.Height),_image.Width,_image.Height);
        }
        else
        {
            _offFrame.AsSpan().Clear();
            fixed(uint* p=_offFrame)LampPainter.Draw(p,W,H,W/2+MathF.Cos(frame*MathF.Tau/Count)*1000,78+MathF.Sin(frame*MathF.Tau/Count)*1000,false);
            LampRaster.Copy(_offFrame,new Span<uint>(_image.Pixels,_image.Width*_image.Height),_image.Width,_image.Height);
        }
        _image.Upload(_layout.Left,_layout.Top);
    }
    internal void Show(){_visible=true;_image.Show();}
    internal void Hide(){_visible=false;_image.Hide();}
    public void Dispose(){Native.KillTimer(_image.Window,1);if(_current==this)_current=null;_visible=false;_image.Dispose();}
}

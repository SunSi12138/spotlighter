using System;
namespace Spotlighter;

// Last-resort Win32 fallback when DirectComposition is unavailable.
// A single retained bitmap covers every possible cursor position. It uses more
// memory than the normal CPU compositor path, but never joins moving windows.
internal sealed unsafe class DesktopSurface : IDesktopOverlay
{
    private readonly string _class;
    private readonly nint _module;
    private OverlayBitmap? _image;
    private bool _visible,_disposed;
    private LightSettings? _settings;
    private float _size,_brightness,_softness,_ambient,_temperature;
    private int _x=int.MinValue,_y;
    public int Left {get;}
    public int Top {get;}
    public int Width {get;}
    public int Height {get;}
    internal DesktopSurface(string cls,nint module)
    {
        _class=cls;_module=module;
        Left=Native.GetSystemMetrics(76);Top=Native.GetSystemMetrics(77);
        Width=Native.GetSystemMetrics(78);Height=Native.GetSystemMetrics(79);
        if(FrameMetrics.TestWidth>0)Width=Math.Min(Width,FrameMetrics.TestWidth);
        if(FrameMetrics.TestHeight>0)Height=Math.Min(Height,FrameMetrics.TestHeight);
        if(Width<1||Height<1||(long)Width*Height>16_000_000)throw new InvalidOperationException("Win32 fallback desktop exceeds its memory limit");
        FrameMetrics.Adapter="CPU / Win32 retained bitmap";FrameMetrics.Mode="CPU single-layer compatibility fallback";
        Build();
    }
    private void Build()
    {
        LightSettings s=LightSettings.Current;
        OverlayBitmap replacement=new(_class,_module,Width*2,Height*2);
        try{
            float radius=LightMask.Radius(Width,Height,s);
            LightMask.Tint color=new(s.Temperature);
            for(int y=0;y<replacement.Height;y++)for(int x=0;x<replacement.Width;x++)
                replacement.Pixels[y*replacement.Width+x]=LightMask.Pixel(x-Width+.5f,y-Height+.5f,radius,s,color);
            replacement.Upload(Left-Width,Top-Height);
        }catch{replacement.Dispose();throw;}
        _image?.Dispose();_image=replacement;
        _settings=s;_size=s.Size;_brightness=s.Brightness;_softness=s.Softness;_ambient=s.Ambient;_temperature=s.Temperature;_x=int.MinValue;
    }
    public void Activate(){_visible=true;Native.GetCursorPos(out Native.POINT p);_x=int.MinValue;Render(p.X,p.Y);_image!.Show();}
    public void Deactivate(){_visible=false;_image?.Hide();}
    public void Render(float x,float y)
    {
        if(_disposed)return;
        LightSettings s=LightSettings.Current;
        if(_settings!=s||_size!=s.Size||_brightness!=s.Brightness||_softness!=s.Softness||_ambient!=s.Ambient||_temperature!=s.Temperature){Build();if(_visible)_image!.Show();}
        int cx=(int)MathF.Round(Math.Clamp(x,Left,Left+Width)),cy=(int)MathF.Round(Math.Clamp(y,Top,Top+Height));
        if(cx==_x&&cy==_y)return;_x=cx;_y=cy;_image!.Move(cx-Width,cy-Height);
    }
    internal void Raise(){if(_image!=null)_image.Show();}
    public void Dispose(){if(_disposed)return;_disposed=true;_image?.Dispose();}
}

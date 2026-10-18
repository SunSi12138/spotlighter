using System;
namespace Spotlighter;

// Retained layered-window bitmap; upload only when content actually changes.
internal sealed unsafe class OverlayBitmap : IDisposable
{
    internal readonly nint Window;
    private bool _disposed;
    private nint _dc, _bitmap, _old;
    internal uint* Pixels;
    internal readonly int Width, Height;
    internal OverlayBitmap(string cls,nint module,int width,int height,bool interactive=false,bool activate=false,string title="Spotlighter light layer")
    {
        Width=width;Height=height;
        try
        {
            Window=Native.CreateWindowExW(Native.WS_EX_LAYERED|(interactive?0u:Native.WS_EX_TRANSPARENT)|(activate?0u:Native.WS_EX_NOACTIVATE)|Native.WS_EX_TOPMOST|Native.WS_EX_TOOLWINDOW,
                cls,title,Native.WS_POPUP,0,0,width,height,0,0,module,0);
            if(Window==0) throw new InvalidOperationException("Cannot create light layer");
            _dc=Native.CreateCompatibleDC(0);
            Native.BITMAPINFO info=new(){bmiHeader=new(){biSize=(uint)sizeof(Native.BITMAPINFOHEADER),biWidth=width,biHeight=-height,biPlanes=1,biBitCount=32}};
            _bitmap=Native.CreateDIBSection(_dc,ref info,0,out nint bits,0,0);
            if(_dc==0||_bitmap==0) throw new InvalidOperationException("Cannot allocate light mask");
            Pixels=(uint*)bits;_old=Native.SelectObject(_dc,_bitmap);
        }
        catch { Dispose();throw; }
    }
    internal void Upload(int x,int y)
    {
        Native.POINT pos=new(x,y),origin=new(0,0);Native.SIZE size=new(Width,Height);
        Native.BLENDFUNCTION blend=new(){SourceConstantAlpha=255,AlphaFormat=1};
        if(!Native.UpdateLayeredWindow(Window,0,ref pos,ref size,_dc,ref origin,0,ref blend,Native.ULW_ALPHA))
            throw new InvalidOperationException("Light bitmap upload failed");
    }
    internal void Move(int x,int y) => Native.SetWindowPos(Window,0,x,y,0,0,0x15);
    internal void Show() => Native.SetWindowPos(Window,Native.HWND_TOPMOST,0,0,0,0,0x53);
    internal void Hide() => Native.ShowWindow(Window,0);
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        if(Window!=0) Native.DestroyWindow(Window);
        if(_old!=0) Native.SelectObject(_dc,_old);
        if(_bitmap!=0) Native.DeleteObject(_bitmap);
        if(_dc!=0) Native.DeleteDC(_dc);
        _dc=_bitmap=_old=0;Pixels=null;
    }
}

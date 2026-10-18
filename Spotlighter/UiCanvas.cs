using System;
using System.Runtime.InteropServices;
namespace Spotlighter;

// GDI+ paints only this small popup, on interaction; never a desktop frame.
internal sealed unsafe class UiCanvas : IDisposable
{
    private static nuint _token;
    private readonly nint _bitmap,_graphics,_family;
    private readonly float _scale;
    [StructLayout(LayoutKind.Sequential)] private struct Startup { public uint Version;public nint Callback;public int SuppressThread,SuppressCodecs; }
    [StructLayout(LayoutKind.Sequential)] private struct RectF { public float X,Y,W,H; }
    [DllImport("gdiplus.dll")] private static extern int GdiplusStartup(out nuint token,ref Startup input,nint output);
    [DllImport("gdiplus.dll")] private static extern int GdipCreateBitmapFromScan0(int w,int h,int stride,int format,nint data,out nint bitmap);
    [DllImport("gdiplus.dll")] private static extern int GdipGetImageGraphicsContext(nint bitmap,out nint graphics);
    [DllImport("gdiplus.dll")] private static extern int GdipSetSmoothingMode(nint graphics,int mode);
    [DllImport("gdiplus.dll")] private static extern int GdipSetTextRenderingHint(nint graphics,int hint);
    [DllImport("gdiplus.dll",CharSet=CharSet.Unicode)] private static extern int GdipCreateFontFamilyFromName(string name,nint collection,out nint family);
    [DllImport("gdiplus.dll")] private static extern int GdipCreateFont(nint family,float size,int style,int unit,out nint font);
    [DllImport("gdiplus.dll")] private static extern int GdipCreateSolidFill(uint color,out nint brush);
    [DllImport("gdiplus.dll")] private static extern int GdipFillRectangle(nint g,nint brush,float x,float y,float w,float h);
    [DllImport("gdiplus.dll")] private static extern int GdipFillEllipse(nint g,nint brush,float x,float y,float w,float h);
    [DllImport("gdiplus.dll",CharSet=CharSet.Unicode)] private static extern int GdipDrawString(nint g,string text,int length,nint font,ref RectF rect,nint format,nint brush);
    [DllImport("gdiplus.dll")] private static extern int GdipCreatePath(int fill,out nint path);
    [DllImport("gdiplus.dll")] private static extern int GdipAddPathArc(nint path,float x,float y,float w,float h,float start,float sweep);
    [DllImport("gdiplus.dll")] private static extern int GdipClosePathFigure(nint path);
    [DllImport("gdiplus.dll")] private static extern int GdipFillPath(nint g,nint brush,nint path);
    [DllImport("gdiplus.dll")] private static extern int GdipDeletePath(nint p);
    [DllImport("gdiplus.dll")] private static extern int GdipDeleteBrush(nint p);
    [DllImport("gdiplus.dll")] private static extern int GdipDeleteFont(nint p);
    [DllImport("gdiplus.dll")] private static extern int GdipDeleteFontFamily(nint p);
    [DllImport("gdiplus.dll")] private static extern int GdipDeleteGraphics(nint p);
    [DllImport("gdiplus.dll")] private static extern int GdipDisposeImage(nint p);
    [DllImport("gdiplus.dll",CharSet=CharSet.Unicode)] private static extern int GdipSaveImageToFile(nint bitmap,string path,ref Guid encoder,nint parameters);
    internal static void SavePng(OverlayBitmap image,string path)
    {
        Check(GdipCreateBitmapFromScan0(image.Width,image.Height,image.Width*4,0xe200b,(nint)image.Pixels,out nint bitmap));
        try{Guid png=new("557cf406-1a04-11d3-9a73-0000f81ef32e");Check(GdipSaveImageToFile(bitmap,path,ref png,0));}
        finally{GdipDisposeImage(bitmap);}
    }
    private static void Check(int status){if(status!=0)throw new InvalidOperationException("Popup drawing failed: "+status);}
    internal UiCanvas(OverlayBitmap image,float scale)
    {
        if(_token==0){Startup input=new(){Version=1};Check(GdiplusStartup(out _token,ref input,0));}
        _scale=scale;
        new Span<uint>(image.Pixels,image.Width*image.Height).Clear();
        Check(GdipCreateBitmapFromScan0(image.Width,image.Height,image.Width*4,0xe200b,(nint)image.Pixels,out _bitmap));
        Check(GdipGetImageGraphicsContext(_bitmap,out _graphics));
        Check(GdipCreateFontFamilyFromName("Segoe UI",0,out _family));
        GdipSetSmoothingMode(_graphics,4);GdipSetTextRenderingHint(_graphics,4);
    }
    internal void Round(float x,float y,float w,float h,float radius,uint color)
    {
        x*=_scale;y*=_scale;w*=_scale;h*=_scale;float d=radius*2*_scale;
        Check(GdipCreatePath(0,out nint path));Check(GdipCreateSolidFill(color,out nint brush));
        try{
            GdipAddPathArc(path,x,y,d,d,180,90);GdipAddPathArc(path,x+w-d,y,d,d,270,90);
            GdipAddPathArc(path,x+w-d,y+h-d,d,d,0,90);GdipAddPathArc(path,x,y+h-d,d,d,90,90);
            GdipClosePathFigure(path);Check(GdipFillPath(_graphics,brush,path));
        }finally{GdipDeleteBrush(brush);GdipDeletePath(path);}
    }
    internal void Circle(float x,float y,float radius,uint color)
    {
        Check(GdipCreateSolidFill(color,out nint brush));
        try{GdipFillEllipse(_graphics,brush,(x-radius)*_scale,(y-radius)*_scale,radius*2*_scale,radius*2*_scale);}
        finally{GdipDeleteBrush(brush);}
    }
    internal void Text(string text,float x,float y,float w,float size,uint color,bool bold=false)
    {
        Check(GdipCreateFont(_family,size*_scale,bold?1:0,2,out nint font));
        Check(GdipCreateSolidFill(color,out nint brush));
        try{RectF rect=new(){X=x*_scale,Y=y*_scale,W=w*_scale,H=(size+12)*_scale};Check(GdipDrawString(_graphics,text,-1,font,ref rect,0,brush));}
        finally{GdipDeleteBrush(brush);GdipDeleteFont(font);}
    }
    public void Dispose(){if(_family!=0)GdipDeleteFontFamily(_family);if(_graphics!=0)GdipDeleteGraphics(_graphics);if(_bitmap!=0)GdipDisposeImage(_bitmap);}
}

using System;
using System.Runtime.InteropServices;

namespace Spotlighter;

// Physical screen pixels: do not multiply by DPI again. Width has no effect
// on the lamp size, so a wider monitor does not produce a bigger lamp.
internal readonly struct LampLayout
{
    internal const int DesignWidth=240,DesignHeight=190;
    internal readonly int Left,Top,Width,Height;
    internal int CenterX=>Left+Width/2;
    internal float PivotY=>Top+78f*Height/DesignHeight;
    internal int PanelX=>CenterX+(int)MathF.Round(135f*Width/DesignWidth);
    internal int PanelY=>Top+(int)MathF.Round(140f*Height/DesignHeight);

    internal LampLayout(int left,int top,int screenWidth,int screenHeight)
    {
        float scale=Math.Max(1,screenHeight)/2160f;
        Width=Math.Max(1,(int)MathF.Round(DesignWidth*scale));
        Height=Math.Max(1,(int)MathF.Round(DesignHeight*scale));
        Left=left+screenWidth/2-Width/2;Top=top;
    }

    // Keep the lamp on a real display instead of the virtual desktop's center,
    // which can fall in a gap between monitors or above a shorter monitor.
    internal static LampLayout Primary()
    {
        MonitorInfo info=new(){Size=(uint)Marshal.SizeOf<MonitorInfo>()};
        if(GetMonitorInfoW(MonitorFromWindow(0,1),ref info))
            return new(info.Monitor.L,info.Monitor.T,info.Monitor.R-info.Monitor.L,info.Monitor.B-info.Monitor.T);
        return new(0,0,Native.GetSystemMetrics(0),Native.GetSystemMetrics(1));
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { internal uint Size;internal Rect Monitor,Work;internal uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window,uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfoW(nint monitor,ref MonitorInfo info);
}

internal static class LampRaster
{
    // Resample only when the cached lamp direction or color changes. Keeping
    // the 48 source frames at design size bounds cache memory at any resolution.
    // Interpolate premultiplied colors directly to avoid transparent dark rims.
    internal static void Copy(ReadOnlySpan<uint> source,Span<uint> target,int width,int height)
    {
        const int sw=LampLayout.DesignWidth,sh=LampLayout.DesignHeight;
        if(width==sw&&height==sh){source.CopyTo(target);return;}
        for(int y=0;y<height;y++)
        {
            float sy=Math.Clamp((y+.5f)*sh/height-.5f,0,sh-1);
            int y0=(int)sy,y1=Math.Min(y0+1,sh-1);float fy=sy-y0;
            for(int x=0;x<width;x++)
            {
                float sx=Math.Clamp((x+.5f)*sw/width-.5f,0,sw-1);
                int x0=(int)sx,x1=Math.Min(x0+1,sw-1);float fx=sx-x0;
                uint a=source[y0*sw+x0],b=source[y0*sw+x1],c=source[y1*sw+x0],d=source[y1*sw+x1],pixel=0;
                for(int shift=0;shift<=24;shift+=8)
                {
                    float top=((a>>shift)&255)*(1-fx)+((b>>shift)&255)*fx;
                    float bottom=((c>>shift)&255)*(1-fx)+((d>>shift)&255)*fx;
                    pixel|=(uint)MathF.Round(top*(1-fy)+bottom*fy)<<shift;
                }
                target[y*width+x]=pixel;
            }
        }
    }
}

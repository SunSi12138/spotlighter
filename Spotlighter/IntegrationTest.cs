using System;
using System.IO;
namespace Spotlighter;

// Opt-in real compositor test. Synchronous readback exists only here.
internal static class IntegrationTest
{
    internal static int Run(string cls,nint module,string output) {
        try {
            LightSettings.Reset();LightSettings.Current.Temperature=0;FrameMetrics.TestWidth=1280;FrameMetrics.TestHeight=720;
            using CompositionOverlay surface=new(cls,module);surface.Activate();
            uint[]? reference=null;int maxError=0;bool stable=true,black=false,center=false;
            foreach(float ambient in new[]{0f,.3f}) {
                LightSettings.Current.Ambient=ambient;surface.Render(640,360);
                uint[] pixels=surface.ReadDiagnostic(null);
                float sx=surface.Width*2f/surface.RenderWidth,sy=surface.Height*2f/surface.RenderHeight;
                for(int y=0;y<surface.RenderHeight;y++)for(int x=0;x<surface.RenderWidth;x++) {
                    uint expected=LightMask.Pixel((x+.5f)*sx-surface.Width,(y+.5f)*sy-surface.Height,
                        LightMask.Radius(surface.Width,surface.Height,LightSettings.Current),LightSettings.Current);
                    uint actual=pixels[y*surface.RenderWidth+x];
                    if((actual&0xffffff)!=0)throw new InvalidOperationException("Nonblack mask RGB");
                    maxError=Math.Max(maxError,Math.Abs((int)(actual>>24)-(int)(expected>>24)));
                }
                if(ambient==0){black=pixels[0]==0xff000000&&pixels[^1]==0xff000000;center=pixels[(surface.RenderHeight/2)*surface.RenderWidth+surface.RenderWidth/2]==0;}
                reference=pixels;
                surface.Render(surface.Left,surface.Top);
                uint[] moved=surface.ReadDiagnostic(null);
                if(!reference.AsSpan().SequenceEqual(moved))stable=false;
            }
            bool valid=maxError==0&&black&&center&&stable;
            File.WriteAllText(output,$"Device: {FrameMetrics.Adapter}\nCached mask: {surface.RenderWidth}x{surface.RenderHeight}\nMaximum alpha error: {maxError}/255\nBlack corners: {black}; transparent center: {center}; movement preserves bitmap: {stable}\nResult: {(valid?"PASS":"FAIL")}\nThis checks cached pixels, not physical display, input, or visual flicker.\n");return valid?0:81;
        }catch(Exception e){File.WriteAllText(output,e.ToString());return 82;}
    }
}

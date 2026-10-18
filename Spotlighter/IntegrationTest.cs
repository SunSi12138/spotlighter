using System;
using System.IO;
namespace Spotlighter;

// Opt-in real compositor test. Synchronous readback exists only here.
internal static class IntegrationTest
{
    internal static int Run(string cls,nint module,string output) {
        try {
            LightSettings.Reset();LightSettings.Current.Temperature=0;FrameMetrics.TestWidth=1280;FrameMetrics.TestHeight=720;
            using CompositionOverlay surface=new(cls,module){CaptureSubmittedFrames=true};surface.Activate();
            uint[]? reference=null;int maxError=0;bool stable=true,black=false,center=false;
            foreach(float ambient in new[]{0f,.3f}) {
                LightSettings.Current.Ambient=ambient;surface.Render(640,360);
                uint[] pixels=surface.ReadDiagnostic();
                float sx=surface.Width*2f/surface.RenderWidth,sy=surface.Height*2f/surface.RenderHeight;
                for(int y=0;y<surface.RenderHeight;y++)for(int x=0;x<surface.RenderWidth;x++) {
                    uint expected=LightMask.NeutralPixel((x+.5f)*sx-surface.Width,(y+.5f)*sy-surface.Height,
                        LightMask.Radius(surface.Width,surface.Height,LightSettings.Current),LightSettings.Current);
                    uint actual=pixels[y*surface.RenderWidth+x];
                    maxError=Math.Max(maxError,Math.Abs((int)(actual>>24)-(int)(expected>>24)));
                }
                if(ambient==0){black=pixels[0]==0xff000000&&pixels[^1]==0xff000000;center=pixels[(surface.RenderHeight/2)*surface.RenderWidth+surface.RenderWidth/2]==0;}
                reference=pixels;
                surface.Render(surface.Left,surface.Top);
                uint[] moved=surface.ReadDiagnostic();
                if(!reference.AsSpan().SequenceEqual(moved))stable=false;
            }
            int colorError=0;
            int uploadsBeforeSweep=surface.MaskUploadCount;
            foreach(float temperature in new[]{-1f,-.75f,-.5f,-.25f,0f,.1f,.2f,.4f,.6f,.8f,1f,0f,-1f})
            {
                LightSettings.Current.Temperature=temperature;surface.Render(640,360);
                uint[] actual=surface.ReadDiagnostic();
                float sx=surface.Width*2f/surface.RenderWidth,sy=surface.Height*2f/surface.RenderHeight;
                for(int y=0;y<surface.RenderHeight;y++)for(int x=0;x<surface.RenderWidth;x++)
                {
                    uint expected=LightMask.NeutralPixel((x+.5f)*sx-surface.Width,(y+.5f)*sy-surface.Height,LightMask.Radius(surface.Width,surface.Height,LightSettings.Current),LightSettings.Current);
                    for(int shift=0;shift<=24;shift+=8)colorError=Math.Max(colorError,Math.Abs((int)((expected>>shift)&255)-(int)((actual[y*surface.RenderWidth+x]>>shift)&255)));
                }
            }
            bool tintOnly=surface.MaskUploadCount==uploadsBeforeSweep&&surface.TintUpdateCount>=13;
            bool valid=maxError==0&&colorError==0&&black&&center&&stable&&tintOnly;
            string display=TintDisplayProbe.Run(surface,module,out int displayError);
            if(displayError>1)valid=false;
            File.WriteAllText(output,$"Device: {FrameMetrics.Adapter}\nNeutral mask: {surface.RenderWidth}x{surface.RenderHeight}\nMaximum submitted mask channel error: {colorError}/255\nNeutral black corners: {black}; transparent center: {center}; movement preserves bitmap: {stable}\nTemperature-only changes avoid mask rebuild/upload: {tintOnly}\nTint opacity updates: {surface.TintUpdateCount}\nResult: {(valid?"PASS":"FAIL")}\nReadback is captured before Present, not from a rotated next-frame buffer. Mask checks alone do not prove DWM final pixels, physical display, input, or visual flicker.\n\n{display}");return valid?0:81;
        }catch(Exception e){File.WriteAllText(output,e.ToString());return 82;}
    }
}

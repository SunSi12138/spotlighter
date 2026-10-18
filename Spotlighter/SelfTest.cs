using System;
using System.Diagnostics;
using System.IO;
namespace Spotlighter;
internal static unsafe class SelfTest
{
    internal static int Run() {
        LightSettings.Reset();
        LightSettings s=LightSettings.Current;
        if(s.Ambient!=.01f||s.Temperature!=0)return 16;
        s.Ambient=0;s.Temperature=0;
        if(LightMask.Pixel(10000,10000,100,s)!=0xff000000 || LightMask.Pixel(0,0,100,s)!=0) return 10;
        foreach(float softness in new[]{0f,1f,2.5f}) {
            s.Softness=softness;float last=1;
            for(int x=0;x<1000;x++) {
                float t=LightMask.Transmission(x,0,100,s);
                if(!float.IsFinite(t)||t<0||t>1||t>last+.00001f)return 11;last=t;
            }
        }
        s.Ambient=.3f;
        if(MathF.Abs(LightMask.Transmission(10000,0,100,s)-.3f)>.00001f)return 12;
        s.Size=float.NaN;s.Brightness=float.PositiveInfinity;s.Clamp();
        if(s.Size!=1||s.Brightness!=1)return 13;
        uint[] pixels=new uint[240*190];int painted=0;
        fixed(uint* p=pixels)LampPainter.Draw(p,240,190,120,1000);
        foreach(uint p in pixels){uint a=p>>24;if((p&255)>a||((p>>8)&255)>a||((p>>16)&255)>a)return 14;if(a>0)painted++;}
        foreach(float temperature in new[]{-1f,-.5f,0f,.5f,1f})
        {
            s.Temperature=temperature;s.Ambient=0;
            if(LightMask.Pixel(10000,10000,100,s)!=0xff000000)return 17;
            foreach(float ambient in new[]{0f,.01f,.3f})
            {
                s.Ambient=ambient;
                for(int x=0;x<500;x++){uint p=LightMask.Pixel(x,0,100,s),a=p>>24;if((p&255)>a||((p>>8)&255)>a||((p>>16)&255)>a)return 18;}
            }
        }
        // At zero, the mask is exactly neutral at center, penumbra and outside.
        s.Ambient=0;s.Temperature=0;s.Brightness=1;s.Softness=0;
        if(LightMask.Pixel(0,0,100,s)!=0 || LightMask.Pixel(66,0,100,s)!=0x80000000 || LightMask.Pixel(10000,0,100,s)!=0xff000000)return 19;
        s.Ambient=.15f;s.Temperature=1;uint warm=LightMask.Pixel(10000,0,100,s);
        s.Temperature=-1;uint cool=LightMask.Pixel(10000,0,100,s);
        int wr=White(warm,16),wb=White(warm,0),cr=White(cool,16),cb=White(cool,0);
        if(wr-wb<10 || cb-cr<10)return 20;
        s.Temperature=float.NaN;s.Clamp();if(s.Temperature!=0)return 21;
        // Softness extends the lit ring: this region must inherit the hue.
        s.Ambient=0;s.Softness=0;s.Temperature=1;
        if(LightMask.Pixel(180,0,100,s)!=0xff000000)return 22;
        s.Softness=2.5f;uint fringeWarm=LightMask.Pixel(180,0,100,s);
        s.Temperature=-1;uint fringeCool=LightMask.Pixel(180,0,100,s);
        if(White(fringeWarm,16)-White(fringeWarm,0)<12 || White(fringeCool,0)-White(fringeCool,16)<10)return 23;
        // Neutral has no hard +/-0.04 threshold or sudden color response.
        if(LightMask.TintAmount(0)!=0||LightMask.TintAmount(1)!=1||LightMask.TintAmount(-1)!=1)return 24;
        if(LightMask.TintAmount(.04f)>.001f||LightMask.TintAmount(-.04f)>.001f)return 25;
        foreach(float sign in new[]{-1f,1f}){float previous=0;for(int i=0;i<=1000;i++){float value=LightMask.TintAmount(sign*i/1000f);if(value<previous||!float.IsFinite(value))return 26;previous=value;}}
        foreach(float ambient in new[]{0f,.01f,.15f})foreach(int x in new[]{0,66,120,180})
        {
            s.Ambient=ambient;s.Temperature=0;uint neutral=LightMask.Pixel(x,0,100,s);
            foreach(float temperature in new[]{-.04f,.04f}){s.Temperature=temperature;uint tinted=LightMask.Pixel(x,0,100,s);foreach(int shift in new[]{0,8,16})if(Math.Abs(White(neutral,shift)-White(tinted,shift))>1)return 27;}
        }
        LampClickGesture click=new();if(click.Release())return 28;
        click.Press();if(!click.Release()||!click.FireSingle()||click.FireSingle())return 29;
        click.Press();if(!click.Release())return 30;click.DoubleClick();if(click.Release()||click.FireSingle())return 31;
        click.Press();if(!click.Release()||!click.FireSingle())return 32;
        // The visible bulb follows the same warm/neutral/cool direction.
        uint[] bulbs=new uint[3];uint[] pivots=new uint[3];
        int colorIndex=0;
        foreach(float temperature in new[]{-1f,0f,1f})
        {
            pixels.AsSpan().Clear();fixed(uint* p=pixels)LampPainter.Draw(p,240,190,120,1000,true,temperature);
            bulbs[colorIndex]=pixels[115*240+120];pivots[colorIndex++]=pixels[78*240+120];
            foreach(uint p in pixels){uint a=p>>24;if((p&255)>a||((p>>8)&255)>a||((p>>16)&255)>a)return 33;}
        }
        if((bulbs[0]&255)<=((bulbs[0]>>16)&255)+60 || ((bulbs[2]>>16)&255)<=(bulbs[2]&255)+100)return 34;
        if((bulbs[0]&255)<=(bulbs[1]&255)||(bulbs[1]&255)<=(bulbs[2]&255))return 35;
        if(pivots[0]!=pivots[1]||pivots[1]!=pivots[2])return 36;
        pixels.AsSpan().Clear();fixed(uint* p=pixels)LampPainter.Draw(p,240,190,120,1000,false,-1);uint[] off=(uint[])pixels.Clone();
        pixels.AsSpan().Clear();fixed(uint* p=pixels)LampPainter.Draw(p,240,190,120,1000,false,1);if(!off.AsSpan().SequenceEqual(pixels))return 37;
        // Physical height controls both bitmap geometry and its hit-test pixels.
        // A 4K frame is unchanged; 1080p is exactly half size, including on 21:9.
        LampLayout full=new(0,0,3840,2160),half=new(0,0,1920,1080),wide=new(-2560,-120,2560,1080);
        if(full.Width!=240||full.Height!=190||half.Width!=120||half.Height!=95)return 38;
        if(wide.Width!=half.Width||wide.Height!=half.Height||wide.CenterX!=-1280||wide.Top!=-120)return 39;
        if(full.PanelX!=2055||full.PanelY!=140||half.PanelY!=70||half.PivotY!=39)return 40;
        pixels.AsSpan().Clear();fixed(uint* p=pixels)LampPainter.Draw(p,240,190,120,1000,true,-1);
        foreach(int screenHeight in new[]{720,1080,1440,2160,4320})
        {
            LampLayout layout=new(0,0,3840,screenHeight);
            uint[] scaled=new uint[layout.Width*layout.Height];LampRaster.Copy(pixels,scaled,layout.Width,layout.Height);
            if(MathF.Abs(layout.Width-240f*screenHeight/2160)>.5f||MathF.Abs(layout.Height-190f*screenHeight/2160)>.5f)return 41;
            if(screenHeight==2160&&!pixels.AsSpan().SequenceEqual(scaled))return 42;
            // Pivot and bulb stay clickable after resizing; transparent margins pass through.
            if((scaled[(int)(78f*layout.Height/190)*layout.Width+layout.Width/2]>>24)<64||scaled[^1]!=0)return 43;
            uint bulb=scaled[(int)(115f*layout.Height/190)*layout.Width+layout.Width/2];
            if((bulb>>24)<64||(bulb&255)<=((bulb>>16)&255))return 44;
            foreach(uint p in scaled){uint a=p>>24;if((p&255)>a||((p>>8)&255)>a||((p>>16)&255)>a)return 45;}
        }
        LampLayout wide1440=new(0,0,3440,1440),normal1440=new(0,0,2560,1440);
        if(wide1440.Width!=normal1440.Width||wide1440.Height!=normal1440.Height)return 46;
        nint icon=LampIcon.Create();LampIcon.DestroyIcon(icon);
        return painted>100?0:15;
    }
    private static int White(uint p,int shift)=>255-(int)(p>>24)+(int)((p>>shift)&255);
    internal static int Benchmark(string path) {
        LightSettings.Reset(); long started=Stopwatch.GetTimestamp();uint checksum=0;
        for(int y=0;y<1080;y++)for(int x=0;x<1920;x++)checksum^=LightMask.Pixel(x-960,y-540,410,LightSettings.Current);
        File.WriteAllText(path,$"Mask construction microbenchmark: {FrameMetrics.Milliseconds(started):F3} ms; checksum={checksum:X8}. Not desktop FPS; mask construction runs only on settings changes.\n");return 0;
    }
}

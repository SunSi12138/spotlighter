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
        if(LightMask.Pixel(0,0,100,s)!=0 || Math.Abs((int)(LightMask.Pixel(66,0,100,s)>>24)-128)>1 || LightMask.Pixel(10000,0,100,s)!=0xff000000)return 19;
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
        // Ambient position is cubic; persisted values remain actual brightness.
        if(SettingsWindow.FromSlider(3,0)!=0||SettingsWindow.FromSlider(3,1)!=.3f)return 47;
        if(MathF.Abs(SettingsWindow.FromSlider(3,.1f)-.0003f)>.000001f||MathF.Abs(SettingsWindow.FromSlider(3,.5f)-.0375f)>.000001f)return 48;
        float previousAmbient=0;
        for(int i=0;i<=1000;i++)
        {
            float position=i/1000f,value=SettingsWindow.FromSlider(3,position);
            if(value<previousAmbient||MathF.Abs(SettingsWindow.ToSlider(3,value)-position)>.000001f)return 49;
            previousAmbient=value;
        }
        foreach(float saved in new[]{0f,.00001f,.001f,.01f,.15f,.3f})
            if(MathF.Abs(SettingsWindow.FromSlider(3,SettingsWindow.ToSlider(3,saved))-saved)>.000001f)return 50;
        if(SettingsWindow.AdjustedValue(3,0,-1)!=0||SettingsWindow.AdjustedValue(3,.3f,1)!=.3f)return 51;
        float fineStep=SettingsWindow.AdjustedValue(3,.01f,1);
        if(fineStep<=.01f||fineStep-.01f>=.001f||MathF.Abs(SettingsWindow.AdjustedValue(3,fineStep,-1)-.01f)>.000001f)return 52;
        // Quantize the full-strength tint once; float opacity then varies
        // continuously. Dark blue-gray content must not flip red/blue as the
        // warm slider increases, including around the former 0.4 regression.
        LightSettings tintSettings=new(){Ambient=.0075f};
        uint neutralMask=LightMask.NeutralPixel(0,0,100,tintSettings);
        foreach(float sign in new[]{-1f,1f})
        {
            float previousDifference=-8;bool crossedToWarm=false;
            uint fullTint=LightMask.FullTintPixel(neutralMask,tintSettings.Ambient,sign<0);
            for(int i=0;i<=1000;i++)
            {
                float temperature=sign*i/1000f,amount=LightMask.TintAmount(temperature);
                float red=Composite(neutralMask,fullTint,amount,27,0),blue=Composite(neutralMask,fullTint,amount,35,2),difference=red-blue;
                if(sign*(difference-previousDifference)<-.00001f)return 53;
                previousDifference=difference;
                if(sign>0)
                {
                    if(MathF.Round(red)>MathF.Round(blue))crossedToWarm=true;
                    if(crossedToWarm&&MathF.Round(red)<MathF.Round(blue))return 54;
                }
            }
            if(sign>0&&!crossedToWarm)return 55;
        }
        foreach(float ambient in new[]{0f,.0075f,.3f})foreach(int x in new[]{0,66,180,10000})
        {
            tintSettings.Ambient=ambient;uint encoded=LightMask.NeutralPixel(x,0,100,tintSettings);
            if((encoded&0xffffff)!=0)return 56;
            foreach(bool useCool in new[]{false,true})
            {
                uint fullTint=LightMask.FullTintPixel(encoded,ambient,useCool);uint a=fullTint>>24;
                if((fullTint&255)>a||((fullTint>>8)&255)>a||((fullTint>>16)&255)>a)return 57;
                if(Composite(encoded,fullTint,0,255,0)!=255*(1-(encoded>>24)/255f))return 58;
            }
        }
        // Ambient is spatially uniform. Weak fixed grayscale dithering is
        // restricted to the lit transition and never randomizes tint RGB.
        tintSettings.Ambient=.0137f;double sum=0;int low=255,high=0;
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float noise=LightMask.Noise(x,y);if(noise<0||noise>=1||noise!=LightMask.Noise(x,y))return 59;
            int a=(int)(LightMask.NeutralPixel(10000+x,10000+y,100,tintSettings)>>24);
            sum+=a;low=Math.Min(low,a);high=Math.Max(high,a);
            if(LightMask.MaskNoise(tintSettings.Ambient,tintSettings.Ambient,x,y)!=.5f)return 60;
            float dither=LightMask.MaskNoise(.5f,tintSettings.Ambient,x,y);if(dither<.25f||dither>=.75f)return 61;
        }
        if(high!=low||high!=(int)MathF.Floor(255*(1-tintSettings.Ambient)+.5f))return 62;
        foreach(float temperature in new[]{-1f,0f,1f})
        {
            tintSettings.Temperature=temperature;uint expected=LightMask.Pixel(10000,10000,100,tintSettings);
            for(int i=0;i<256;i++)if(LightMask.Pixel(10000+i,10000,100,tintSettings)!=expected)return 63;
        }

        foreach((int width,int height,uint dpi) in new[]{(1920,1080,96u),(3840,2160,144u),(3440,1440,144u),(1280,720,192u)})
        {
            WelcomeLayout guide=new(-1920,-120,width,height,dpi);
            if(guide.Left< -1920||guide.Top< -120||guide.Left+guide.Width> -1920+width||guide.Top+guide.Height> -120+height)return 64;
            if(guide.Width<1||guide.Height<1||!float.IsFinite(guide.Scale))return 65;
        }
        string guideFolder=Path.Combine(Path.GetTempPath(),"SpotlighterGuideTest-"+Guid.NewGuid().ToString("N"));
        string guideState=Path.Combine(guideFolder,"welcome.version");
        try
        {
            if(!WelcomeState.NeedsGuide(guideState)||!WelcomeState.MarkSeen(guideState)||WelcomeState.NeedsGuide(guideState))return 66;
            LightSettings.Reset();if(WelcomeState.NeedsGuide(guideState))return 67;
            File.WriteAllText(guideState,"0");if(!WelcomeState.NeedsGuide(guideState))return 68;
            File.WriteAllText(guideState,"999");if(WelcomeState.NeedsGuide(guideState))return 69;
            File.WriteAllText(guideState,"invalid");if(!WelcomeState.NeedsGuide(guideState))return 70;
        }
        finally
        {
            if(File.Exists(guideState))File.Delete(guideState);
            if(File.Exists(guideState+".tmp"))File.Delete(guideState+".tmp");
            if(Directory.Exists(guideFolder))Directory.Delete(guideFolder);
        }

        nint icon=LampIcon.Create();LampIcon.DestroyIcon(icon);
        return painted>100?0:15;
    }
    private static int White(uint p,int shift)=>255-(int)(p>>24)+(int)((p>>shift)&255);
    private static float Composite(uint neutral,uint fullTint,float amount,float background,int channel)
    {
        float dark=(neutral>>24)/255f,alpha=(fullTint>>24)/255f;
        float color=(fullTint>>(16-channel*8))&255;
        return (1-dark)*(background*(1-alpha*amount)+color*amount);
    }
    internal static int Benchmark(string path) {
        LightSettings.Reset(); long started=Stopwatch.GetTimestamp();uint checksum=0;
        for(int y=0;y<1080;y++)for(int x=0;x<1920;x++)checksum^=LightMask.Pixel(x-960,y-540,410,LightSettings.Current);
        File.WriteAllText(path,$"Mask construction microbenchmark: {FrameMetrics.Milliseconds(started):F3} ms; checksum={checksum:X8}. Not desktop FPS; mask construction runs only on settings changes.\n");return 0;
    }
}

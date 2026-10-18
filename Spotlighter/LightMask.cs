using System;
namespace Spotlighter;

internal static class LightMask
{
    internal static float Radius(int width,int height,LightSettings s) => Math.Min(Math.Min(width,height)*.38f*s.Size,4094/(1+s.Softness*.55f));
    internal static float Transmission(float x,float y,float radius,LightSettings s)
    {
        float r=MathF.Sqrt(x*x+y*y/(.88f*.88f))/radius;
        float edge=Math.Clamp((r-.32f)/(.68f+s.Softness*.55f),0,1);
        float cone=1-edge*edge*(3-2*edge);
        return Math.Clamp(s.Ambient+s.Brightness*cone,0,1);
    }
    internal static float TintAmount(float value)
    {
        double x=Math.Clamp(Math.Abs((double)value),0,1);
        double smooth=Math.Clamp(x*x*x*(x*(6*x-15)+10),0,1);
        // Zero first/second derivatives at neutral. Warm hues are more
        // conspicuous, so their response rises a little more slowly.
        return (float)(value>0?Math.Pow(smooth,1.2):smooth);
    }
    internal readonly struct Tint
    {
        internal readonly float Warm,Cool;
        internal Tint(float value){float amount=TintAmount(value);Warm=value>0?amount:0;Cool=value<0?amount:0;}
    }
    // Stable, non-periodic quantization noise: no time input, allocations or
    // per-mouse-frame work. Fractional alpha becomes coverage rather than rings.
    internal static float Noise(int x,int y)
    {
        uint h=unchecked((uint)x*0x9e3779b9u^(uint)y*0x85ebca6bu);
        h^=h>>16;h=unchecked(h*0x7feb352du);h^=h>>15;h=unchecked(h*0x846ca68bu);h^=h>>16;
        return (h>>8)*(1f/16777216f);
    }
    internal static float MaskNoise(float light,float ambient,int x,int y)
    {
        // Leave the uniform ambient floor and the darkest tail completely
        // uniform. Only the visible light transition gets weak grayscale
        // dithering; colored channels are never independently randomized.
        if(light>=1)return .5f;
        float ramp=Math.Clamp((light-ambient-.02f)/.1f,0,1);
        if(ramp<=0)return .5f;
        float amount=.5f*ramp*ramp*(3-2*ramp);
        return .5f+(Noise(x,y)-.5f)*amount;
    }
    internal static uint NeutralPixel(float x,float y,float radius,LightSettings s)
    {
        float light=Transmission(x,y,radius,s);
        return (uint)Quantize(255*(1-light),MaskNoise(light,s.Ambient,(int)x,(int)y))<<24;
    }
    // Rasterize a full-strength tint once. Only float visual opacity changes
    // during a same-side temperature drag; final colors are not re-quantized.
    internal static uint FullTintPixel(uint neutral,float ambient,bool cool)
    {
        float strength=.18f+.65f*Math.Clamp((neutral>>24)/255f+ambient,0,1);
        int a=Quantize(255*strength,.5f),r=Quantize((cool?150:255)*strength,.5f),g=Quantize((cool?200:180)*strength,.5f),b=Quantize((cool?255:110)*strength,.5f);
        return (uint)(a<<24|Math.Min(a,r)<<16|Math.Min(a,g)<<8|Math.Min(a,b));
    }
    internal static uint Pixel(float x,float y,float radius,LightSettings s) => Pixel(x,y,radius,s,new Tint(s.Temperature));
    internal static uint Pixel(float x,float y,float radius,LightSettings s,in Tint color)
    {
        float light=Transmission(x,y,radius,s);
        // A low-strength colored optical filter. This is a visual temperature
        // approximation; it does not alter the monitor's calibration or gamma.
        float warm=color.Warm,cool=color.Cool;
        // One continuous response covers direct falloff, the expanded fringe
        // and environment light. No threshold or separate neutral outer ring.
        // Fully unlit pixels stay black because every channel scales by light.
        float fringe=Math.Clamp(1-light+s.Ambient,0,1);
        float strength=.18f+.65f*fringe;
        float tint=(warm+cool)*strength;
        float noise=MaskNoise(light,s.Ambient,(int)x,(int)y);
        int a=Quantize(255*(1-light*(1-tint)),noise);
        int r=Quantize(light*(strength*(warm*255+cool*150)),.5f);
        int g=Quantize(light*(strength*(warm*180+cool*200)),.5f);
        int b=Quantize(light*(strength*(warm*110+cool*255)),.5f);
        return (uint)(a<<24 | Math.Min(a,r)<<16 | Math.Min(a,g)<<8 | Math.Min(a,b));
    }
    private static int Quantize(float value,float noise)=>(int)Math.Clamp(value+noise,0,255);
}

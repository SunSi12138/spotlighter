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
        int a=Byte(255*(1-light*(1-tint)));
        int r=Byte(light*(strength*(warm*255+cool*150)));
        int g=Byte(light*(strength*(warm*180+cool*200)));
        int b=Byte(light*(strength*(warm*110+cool*255)));
        return (uint)(a<<24 | Math.Min(a,r)<<16 | Math.Min(a,g)<<8 | Math.Min(a,b));
    }
    private static int Byte(float x)=>(int)Math.Clamp(MathF.Round(x),0,255);
}

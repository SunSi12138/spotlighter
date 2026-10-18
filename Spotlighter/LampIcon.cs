using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Spotlighter;

internal static unsafe class LampIcon
{
    [DllImport("user32.dll")] private static extern nint CreateIconFromResourceEx(byte[] data,uint size,bool icon,uint version,int w,int h,uint flags);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    internal static nint Create()
    {
        byte[] image=Image(32);
        nint icon=CreateIconFromResourceEx(image,(uint)image.Length,true,0x30000,32,32,0);
        if(icon==0)throw new InvalidOperationException("Cannot create lamp icon");
        return icon;
    }
    internal static uint[] Pixels(int size)
    {
        const int sourceW=240,sourceH=190;
        uint[] source=new uint[sourceW*sourceH],result=new uint[size*size];
        fixed(uint* p=source)LampPainter.Draw(p,sourceW,sourceH,700,950);
        const int cropX=60,cropY=18,cropSize=120;
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            uint a=0,r=0,g=0,b=0;const int samples=4;
            for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
            {
                int px=cropX+(int)((x+(sx+.5f)/samples)*cropSize/size);
                int py=cropY+(int)((y+(sy+.5f)/samples)*cropSize/size);
                uint p=source[py*sourceW+px];a+=p>>24;r+=(p>>16)&255;g+=(p>>8)&255;b+=p&255;
            }
            result[y*size+x]=(a/16)<<24|(r/16)<<16|(g/16)<<8|b/16;
        }
        return result;
    }
    private static byte[] Image(int size)
    {
        uint[] pixels=Pixels(size);
        using MemoryStream stream=new();using BinaryWriter writer=new(stream);
        int maskStride=(size+31)/32*4;
        writer.Write(40);writer.Write(size);writer.Write(size*2);writer.Write((ushort)1);writer.Write((ushort)32);
        writer.Write(0);writer.Write(size*size*4+maskStride*size);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
        for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++)
        {
            uint p=pixels[y*size+x],a=p>>24;
            uint r=a==0?0:Math.Min(255,((p>>16)&255)*255/a);
            uint g=a==0?0:Math.Min(255,((p>>8)&255)*255/a);
            uint b=a==0?0:Math.Min(255,(p&255)*255/a);
            writer.Write(a<<24|r<<16|g<<8|b);
        }
        for(int y=size-1;y>=0;y--)for(int index=0;index<maskStride;index++)
        {
            byte mask=0;
            for(int bit=0;bit<8;bit++){int x=index*8+bit;if(x<size&&(pixels[y*size+x]>>24)==0)mask|=(byte)(1<<(7-bit));}
            writer.Write(mask);
        }
        return stream.ToArray();
    }
    internal static void Write(string path)
    {
        int[] sizes={16,24,32,48,64};
        byte[][] images=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++)images[i]=Image(sizes[i]);
        using BinaryWriter writer=new(File.Create(path));
        writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
        int offset=6+16*sizes.Length;
        for(int i=0;i<sizes.Length;i++)
        {
            writer.Write((byte)sizes[i]);writer.Write((byte)sizes[i]);writer.Write((ushort)0);
            writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;
        }
        foreach(byte[] image in images)writer.Write(image);
    }
}

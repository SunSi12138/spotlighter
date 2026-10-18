using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Spotlighter;

// Diagnostic-only synchronous readback. Never used by Render or the profiler.
internal static unsafe class CompositionReadback
{
    [StructLayout(LayoutKind.Sequential)] private struct TextureDesc { public uint Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CpuAccess, MiscFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct Mapped { public nint Data; public uint RowPitch, DepthPitch; }
    private static nint V(nint p, int i) => (*(nint**)p)[i];
    internal static uint[] Read(nint device, nint context, nint source, int width, int height, string? filename = null)
    {
        TextureDesc desc = new() { Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1, Format = 87, SampleCount = 1, Usage = 3, CpuAccess = 0x20000 };
        nint staging = 0;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, TextureDesc*, nint, nint*, int>)V(device, 5))(device, &desc, 0, &staging));
        try
        {
            ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)V(context, 47))(context, staging, source);
            Mapped mapped = default;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, Mapped*, int>)V(context, 14))(context, staging, 0, 1, 0, &mapped));
            uint[] pixels = new uint[checked(width * height)];
            try
            {
                for (int y = 0; y < height; y++) new ReadOnlySpan<uint>((void*)(mapped.Data + y * mapped.RowPitch), width).CopyTo(pixels.AsSpan(y * width, width));
            }
            finally { ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)V(context, 15))(context, staging, 0); }
            if (filename != null)
            {
                using BinaryWriter writer = new(File.Create(filename));
                writer.Write((ushort)0x4d42); writer.Write(54 + width * height * 4); writer.Write(0); writer.Write(54);
                writer.Write(40); writer.Write(width); writer.Write(-height); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(0); writer.Write(width * height * 4); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                writer.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
            }
            return pixels;
        }
        finally { if(staging!=0)((delegate* unmanaged[Stdcall]<nint,uint>)V(staging,2))(staging); }
    }
}

using System;
using System.Runtime.InteropServices;

namespace Spotlighter;

/// <summary>
/// CPU-generated retained light mask -> DirectComposition positioning. No capture or custom shader.
/// Native AOT friendly: raw COM vtables, no RCWs/WinRT reflection/NuGet DLLs.
/// </summary>
internal sealed unsafe class CompositionOverlay : IDesktopOverlay
{
    // Every COM vtable index below includes IUnknown and inherited methods.
    // Direct3D11 device context inherits ID3D11DeviceChild (four methods).
    private const uint WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    private const uint DXGI_FORMAT_B8G8R8A8_UNORM = 87;
    private const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
    private const uint DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL = 3;
    private const uint DXGI_ALPHA_MODE_PREMULTIPLIED = 1;
    private static readonly Guid FactoryIid = new("50C83A1C-E072-4C48-87B0-3630FA36A6D0");
    private static readonly Guid DxgiDeviceIid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid DcompIid = new("C37EA93A-E7AA-450D-B16F-9746CB0407F3");
    private static readonly Guid TextureIid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    private nint _window, _device, _context, _factory, _dxgiDevice;
    private nint _swapChain, _dcomp, _target, _visual;
    private nint _opacityEffect,_tintVisual,_darkVisual;
    private nint _tintSwapChain,_tintBackBuffer;
    private uint[]? _tintPixels;
    internal int RenderWidth { get; private set; }
    internal int RenderHeight { get; private set; }
    private nint _backBuffer;
    internal bool CaptureSubmittedFrames;
    private uint[]? _submittedPixels;
    internal int MaskUploadCount { get; private set; }
    internal int TintUpdateCount { get; private set; }
    private bool _visible, _disposed;
    private readonly int _outputWidth,_outputHeight;
    private bool _cacheValid;
    private uint[]? _cachedPixels;
    private LightSettings? _cachedSettings;
    private float _cachedSize,_cachedBrightness,_cachedSoftness,_cachedAmbient;
    private float _cachedTemperature=float.NaN;
    private int _cachedTintSign;

    public int Left { get; }
    public int Top { get; }
    public int Width { get; }
    public int Height { get; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DxgiSwapChainDesc1
    {
        public uint Width, Height, Format, Stereo, SampleCount, SampleQuality;
        public uint BufferUsage, BufferCount, Scaling, SwapEffect, AlphaMode, Flags;
    }

    [DllImport("d3d11.dll", EntryPoint = "D3D11CreateDevice", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(nint adapter, uint driverType,
        nint software, uint flags, nint requestedFeatureLevels, uint count,
        uint sdkVersion, out nint device, out uint actualFeatureLevel,
        out nint context);

    [DllImport("dcomp.dll", EntryPoint = "DCompositionCreateDevice", ExactSpelling = true)]
    private static extern int DCompositionCreateDevice(nint dxgiDevice, ref Guid iid, out nint result);

    private static nint Method(nint obj, int index) => (*(nint**)obj)[index];

    private static int Query(nint obj, ref Guid iid, out nint result)
    {
        nint value = 0;
        Guid copy = iid;
        int hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)
            Method(obj, 0))(obj, &copy, &value);
        result = value;
        return hr;
    }

    private static void Release(ref nint obj)
    {
        nint p = obj;
        obj = 0;
        if (p != 0)
            ((delegate* unmanaged[Stdcall]<nint, uint>)Method(p, 2))(p);
    }

    private static void Check(int result, string operation)
    {
        if (result < 0)
            throw new InvalidOperationException(operation + " returned HRESULT 0x" +
                ((uint)result).ToString("X8"));
    }

    internal CompositionOverlay(string className, nint module)
    {
        Left = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
        Top = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
        Width = Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN);
        Height = Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN);
        _outputWidth=Width;_outputHeight=Height;
        if (FrameMetrics.TestWidth > 0) Width = Math.Min(Width, FrameMetrics.TestWidth);
        if (FrameMetrics.TestHeight > 0) Height = Math.Min(Height, FrameMetrics.TestHeight);
        if (Width < 1 || Height < 1 || (long)Width * Height > 36_000_000)
            throw new InvalidOperationException("Unsupported virtual desktop size");

        _window = Native.CreateWindowExW(
            WS_EX_NOREDIRECTIONBITMAP | Native.WS_EX_LAYERED | Native.WS_EX_TOPMOST |
            Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW,
            className, "Spotlighter cached light", Native.WS_POPUP,
            Left, Top, _outputWidth, _outputHeight, 0, 0, module, 0);
        if (_window == 0)
            throw new InvalidOperationException("Could not create DirectComposition window");

        try
        {
            // Layered + transparent supplies documented cross-thread hit-test passthrough.
            if (!Native.SetLayeredWindowAttributes(_window, 0, 255, 2))
                throw new InvalidOperationException("SetLayeredWindowAttributes failed");
            // WARP is used only to upload the cached bitmap when settings change.
            // Mouse movement changes a retained visual offset, never runs a shader.
            Check(D3D11CreateDevice(0, 5, 0, 0x20, 0, 0, 7,
                out _device, out _, out _context), "D3D11CreateDevice");
            // Derive the factory from the D3D11 device's actual adapter.
            // A newly created independent DXGI factory can reject a swap chain
            // for a device that was instantiated through another factory.
            Guid dxgiGuid = DxgiDeviceIid;
            Check(Query(_device, ref dxgiGuid, out _dxgiDevice), "ID3D11Device/IDXGIDevice");
            nint adapter = 0;
            try
            {
                Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)
                    Method(_dxgiDevice, 7))(_dxgiDevice, &adapter), "IDXGIDevice.GetAdapter");
                byte* adapterDesc = stackalloc byte[320];
                Check(((delegate* unmanaged[Stdcall]<nint, void*, int>)Method(adapter, 8))(adapter, adapterDesc), "GetDesc");
                FrameMetrics.Adapter = new string((char*)adapterDesc);
                Guid factoryGuid = FactoryIid;
                nint factory = 0;
                Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)
                    Method(adapter, 6))(adapter, &factoryGuid, &factory),
                    "IDXGIAdapter.GetParent(IDXGIFactory2)");
                _factory = factory;
                FrameMetrics.Mode="CPU cached bitmap + DirectComposition (WARP upload only)";

            }
            finally { Release(ref adapter); }

            const int factor=2;
            RenderWidth=Math.Max(1,(Width*factor+3)/4);RenderHeight=Math.Max(1,(Height*factor+3)/4);
            DxgiSwapChainDesc1 desc = new()
            {
                Width = (uint)RenderWidth,
                Height = (uint)RenderHeight,
                Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleCount = 1,
                SampleQuality = 0,
                BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                Scaling = 0, // Stretch is required for composition
                SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
                AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED
            };

            nint chain = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, DxgiSwapChainDesc1*, nint, nint*, int>)
                Method(_factory, 24))(_factory, _device, &desc, 0, &chain),
                "CreateSwapChainForComposition");
            _swapChain = chain;
            nint tintChain=0;
            Check(((delegate* unmanaged[Stdcall]<nint,nint,DxgiSwapChainDesc1*,nint,nint*,int>)Method(_factory,24))(_factory,_device,&desc,0,&tintChain),"Create tint swap chain");
            _tintSwapChain=tintChain;

            Guid compGuid = DcompIid;
            Check(DCompositionCreateDevice(_dxgiDevice, ref compGuid, out _dcomp),
                "DCompositionCreateDevice");

            nint target = 0, visual = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, int, nint*, int>)
                Method(_dcomp, 6))(_dcomp, _window, 1, &target),
                "IDCompositionDevice.CreateTargetForHwnd");
            _target = target;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)
                Method(_dcomp, 7))(_dcomp, &visual), "IDCompositionDevice.CreateVisual");
            _visual = visual;
            nint opacity=0;
            Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Method(_dcomp,23))(_dcomp,&opacity),"CreateEffectGroup");
            _opacityEffect=opacity;
            _tintVisual=CreateVisual();_darkVisual=CreateVisual();
            Check(((delegate* unmanaged[Stdcall]<nint,nint,int>)Method(_tintVisual,10))(_tintVisual,_opacityEffect),"Set tint opacity effect");
            AddVisual(_visual,_tintVisual);AddVisual(_visual,_darkVisual,_tintVisual);
            float* matrix=stackalloc float[6]{(float)_outputWidth*factor/RenderWidth,0,0,(float)_outputHeight*factor/RenderHeight,0,0};
            Check(((delegate* unmanaged[Stdcall]<nint,float*,int>)Method(_visual,8))(_visual,matrix),"Visual.SetTransform");
            Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)
                Method(_darkVisual, 15))(_darkVisual, _swapChain), "Set dark mask content");
            Check(((delegate* unmanaged[Stdcall]<nint,nint,int>)Method(_tintVisual,15))(_tintVisual,_tintSwapChain),"Set fixed tint content");
            Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)
                Method(_target, 3))(_target, _visual), "IDCompositionTarget.SetRoot");
            Check(((delegate* unmanaged[Stdcall]<nint, int>)
                Method(_dcomp, 3))(_dcomp), "IDCompositionDevice.Commit");

            Guid bufferGuid = TextureIid;
            nint backBuffer = 0;
            Check(((delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)Method(_swapChain, 9))(_swapChain, 0, &bufferGuid, &backBuffer), "GetBuffer");
            _backBuffer = backBuffer;
            nint tintBuffer=0;
            Check(((delegate* unmanaged[Stdcall]<nint,uint,Guid*,nint*,int>)Method(_tintSwapChain,9))(_tintSwapChain,0,&bufferGuid,&tintBuffer),"Get tint buffer");
            _tintBackBuffer=tintBuffer;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Activate()
    {
        if (_visible) return;
        Native.GetCursorPos(out Native.POINT cursor);
        Render(cursor.X, cursor.Y);
        Native.ShowWindow(_window, Native.SW_SHOWNA);
        Native.SetWindowPos(_window, Native.HWND_TOPMOST, Left, Top, _outputWidth, _outputHeight,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        _visible = true;
        _cacheValid=false;Render(cursor.X,cursor.Y);
    }

    public void Deactivate()
    {
        if (!_visible) return;
        Native.ShowWindow(_window, Native.SW_HIDE);
        _visible = false;
    }

    public void Render(float x,float y) { if(!_disposed)RenderCached(x,y); }
    private nint CreateVisual()
    {
        nint visual=0;Check(((delegate* unmanaged[Stdcall]<nint,nint*,int>)Method(_dcomp,7))(_dcomp,&visual),"Create child visual");return visual;
    }
    private static void AddVisual(nint parent,nint child,nint reference=0)
        =>Check(((delegate* unmanaged[Stdcall]<nint,nint,int,nint,int>)Method(parent,16))(parent,child,1,reference),"Add child visual");

    private void RenderCached(float x,float y)
    {
        LightSettings s=LightSettings.Current;
        bool rebuild=!_cacheValid || _cachedSettings!=s || _cachedSize!=s.Size || _cachedBrightness!=s.Brightness || _cachedSoftness!=s.Softness || _cachedAmbient!=s.Ambient;
        if(rebuild)
        {
            _cachedPixels ??= new uint[checked(RenderWidth*RenderHeight)];
            float sx=Width*2f/RenderWidth,sy=Height*2f/RenderHeight,radius=LightMask.Radius(Width,Height,s);
            for(int row=0;row<RenderHeight;row++)
                for(int col=0;col<RenderWidth;col++)
                    _cachedPixels[row*RenderWidth+col]=LightMask.NeutralPixel((col+.5f)*sx-Width,(row+.5f)*sy-Height,radius,s);
            fixed(uint* pixels=_cachedPixels)
                ((delegate* unmanaged[Stdcall]<nint,nint,uint,nint,void*,uint,uint,void>)Method(_context,48))(_context,_backBuffer,0,0,pixels,(uint)(RenderWidth*4),0);
            if(CaptureSubmittedFrames)
            {
                Guid iid=TextureIid;nint current=0;
                Check(((delegate* unmanaged[Stdcall]<nint,uint,Guid*,nint*,int>)Method(_swapChain,9))(_swapChain,0,&iid,&current),"Get current submitted buffer");
                try{_submittedPixels=CompositionReadback.Read(_device,_context,current,RenderWidth,RenderHeight);}
                finally{Release(ref current);}
            }
            Check(((delegate* unmanaged[Stdcall]<nint,uint,uint,int>)Method(_swapChain,8))(_swapChain,0,0),"Present cached CPU mask");
            MaskUploadCount++;
            _cachedSettings=s;_cachedSize=s.Size;_cachedBrightness=s.Brightness;_cachedSoftness=s.Softness;_cachedAmbient=s.Ambient;_cacheValid=true;
        }
        int sign=s.Temperature<0?-1:1;
        if(rebuild||_cachedTintSign!=sign)
        {
            _tintPixels??=new uint[checked(RenderWidth*RenderHeight)];
            for(int row=0;row<RenderHeight;row++)for(int col=0;col<RenderWidth;col++)
            {
                int index=row*RenderWidth+col;
                _tintPixels[index]=LightMask.FullTintPixel(_cachedPixels![index],s.Ambient,sign<0);
            }
            fixed(uint* pixels=_tintPixels)
                ((delegate* unmanaged[Stdcall]<nint,nint,uint,nint,void*,uint,uint,void>)Method(_context,48))(_context,_tintBackBuffer,0,0,pixels,(uint)(RenderWidth*4),0);
            Check(((delegate* unmanaged[Stdcall]<nint,uint,uint,int>)Method(_tintSwapChain,8))(_tintSwapChain,0,0),"Present fixed tint field");
            _cachedTintSign=sign;
        }
        if(_cachedTemperature!=s.Temperature)
        {
            Check(((delegate* unmanaged[Stdcall]<nint,float,int>)Method(_opacityEffect,4))(_opacityEffect,LightMask.TintAmount(s.Temperature)),"Tint.SetOpacity");
            _cachedTemperature=s.Temperature;TintUpdateCount++;
        }
        // One composition transaction moves the entire retained bitmap: no seams.
        float dx=(Math.Clamp(x-Left,0,Width)/Width-1)*_outputWidth,dy=(Math.Clamp(y-Top,0,Height)/Height-1)*_outputHeight;
        Check(((delegate* unmanaged[Stdcall]<nint,float,int>)Method(_visual,4))(_visual,dx),"Visual.SetOffsetX");
        Check(((delegate* unmanaged[Stdcall]<nint,float,int>)Method(_visual,6))(_visual,dy),"Visual.SetOffsetY");
        Check(((delegate* unmanaged[Stdcall]<nint,int>)Method(_dcomp,3))(_dcomp),"Commit CPU mask position");
    }

    internal uint[] ReadDiagnostic() => _submittedPixels??throw new InvalidOperationException("Enable submitted-frame capture before rendering");
    internal void WaitDiagnosticCommit() => Check(((delegate* unmanaged[Stdcall]<nint,int>)Method(_dcomp,4))(_dcomp),"Wait for diagnostic commit");
    internal void Raise() => Native.SetWindowPos(_window,Native.HWND_TOPMOST,0,0,0,0,0x13);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_window != 0) Native.ShowWindow(_window, Native.SW_HIDE);
        Release(ref _backBuffer);Release(ref _tintBackBuffer);
        Release(ref _visual);
        Release(ref _darkVisual);Release(ref _tintVisual);
        Release(ref _opacityEffect);
        Release(ref _target);
        Release(ref _dcomp);
        Release(ref _swapChain);Release(ref _tintSwapChain);
        Release(ref _dxgiDevice);
        Release(ref _factory);
        Release(ref _context);
        Release(ref _device);
        if (_window != 0)
        {
            Native.DestroyWindow(_window);
            _window = 0;
        }
    }
}

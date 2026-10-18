using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Spotlighter;

// Opt-in functional test only: put our own known-color patch under the light
// and sample that one desktop point after composition. Never runs in the app
// render loop and never captures or logs other applications' content.
internal static unsafe class TintDisplayProbe
{
    private static readonly Native.WndProc Callback=Native.DefWindowProcW;
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window,nint dc);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(nint window);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc,int x,int y);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool UnregisterClassW(string name,nint module);
    internal static string Run(CompositionOverlay surface,nint module,out int maximumError)
    {
        maximumError=-1;
        const string name="Spotlighter.TintDiagnostic";
        nint brush=CreateSolidBrush(0x231e1b),window=0,dc=0;
        nint className=Marshal.StringToHGlobalUni(name);bool registered=false;
        try
        {
            Native.WNDCLASSEX cls=new(){cbSize=(uint)sizeof(Native.WNDCLASSEX),lpfnWndProc=Marshal.GetFunctionPointerForDelegate(Callback),hInstance=module,lpszClassName=className,hbrBackground=brush};
            registered=Native.RegisterClassExW(ref cls)!=0;
            if(!registered)return "Desktop color probe: unavailable (test window registration failed).\n";
            int x=Native.GetSystemMetrics(76)+Native.GetSystemMetrics(78)/2,y=Native.GetSystemMetrics(77)+Native.GetSystemMetrics(79)/2;
            window=Native.CreateWindowExW(Native.WS_EX_TOOLWINDOW|Native.WS_EX_NOACTIVATE|Native.WS_EX_TOPMOST,name,"Spotlighter color test",Native.WS_POPUP,x-32,y-32,64,64,0,0,module,0);
            if(window==0)return "Desktop color probe: unavailable (test window creation failed).\n";
            Native.ShowWindow(window,Native.SW_SHOWNA);UpdateWindow(window);surface.Raise();
            dc=GetDC(0);
            LightSettings s=LightSettings.Current;s.Temperature=0;s.Ambient=.0075f;
            surface.Render(surface.Left+surface.Width/2f,surface.Top+surface.Height/2f);
            surface.WaitDiagnosticCommit();DwmFlush();
            uint neutral=GetPixel(dc,x,y);
            if(neutral!=0x231e1b)return "Desktop color probe: UNVERIFIED (known neutral patch was not visible to GDI).\n";
            StringBuilder log=new("Desktop color probe on own RGB(27,30,35) patch:\nTemperature;Actual RGB;Float reference RGB\n");
            uint neutralMask=LightMask.NeutralPixel(0,0,100,s);float dark=(neutralMask>>24)/255f;
            maximumError=0;
            foreach(float temperature in new[]{.39f,.4f,.402f,.405f,.408f,.411f,.42f,.439f,.444f,.46f,-.39f,-.4f,-.42f,-.439f,-.46f,0f})
            {
                s.Temperature=temperature;surface.Render(surface.Left+surface.Width/2f,surface.Top+surface.Height/2f);
                surface.WaitDiagnosticCommit();DwmFlush();
                uint pixel=GetPixel(dc,x,y);if(pixel==uint.MaxValue)return "Desktop color probe: UNVERIFIED (GetPixel failed).\n";
                uint fullTint=LightMask.FullTintPixel(neutralMask,s.Ambient,temperature<0);float amount=LightMask.TintAmount(temperature),alpha=(fullTint>>24)/255f;
                int r=(int)(pixel&255),g=(int)((pixel>>8)&255),b=(int)((pixel>>16)&255);
                int er=(int)MathF.Round((1-dark)*(27*(1-alpha*amount)+((fullTint>>16)&255)*amount)),eg=(int)MathF.Round((1-dark)*(30*(1-alpha*amount)+((fullTint>>8)&255)*amount)),eb=(int)MathF.Round((1-dark)*(35*(1-alpha*amount)+(fullTint&255)*amount));
                maximumError=Math.Max(maximumError,Math.Max(Math.Abs(r-er),Math.Max(Math.Abs(g-eg),Math.Abs(b-eb))));
                log.Append($"{temperature:F3};{r},{g},{b};{er},{eg},{eb}\n");
            }
            s.Ambient=0;s.Temperature=1;surface.Render(surface.Left,surface.Top);
            surface.WaitDiagnosticCommit();DwmFlush();uint unlit=GetPixel(dc,x,y);
            if(unlit==uint.MaxValue)return "Desktop color probe: UNVERIFIED (unlit sample failed).\n";
            int unlitError=Math.Max((int)(unlit&255),Math.Max((int)((unlit>>8)&255),(int)((unlit>>16)&255)));
            maximumError=Math.Max(maximumError,unlitError);
            log.Append($"Ambient zero, outside light: RGB({unlit&255},{(unlit>>8)&255},{(unlit>>16)&255})\n");
            log.Append($"Maximum final RGB error vs float reference: {maximumError}/255. GDI/DWM observation, not physical panel or HDR calibration.\n");
            log.Append($"Desktop color probe result: {(maximumError<=1?"PASS":"FAIL")}\n");
            return log.ToString();
        }
        finally
        {
            if(dc!=0)ReleaseDC(0,dc);
            if(window!=0)Native.DestroyWindow(window);
            if(registered)UnregisterClassW(name,module);
            if(brush!=0)Native.DeleteObject(brush);
            Marshal.FreeHGlobal(className);
        }
    }
}

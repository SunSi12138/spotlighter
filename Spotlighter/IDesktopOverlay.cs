using System;

namespace Spotlighter;

internal interface IDesktopOverlay : IDisposable
{
    int Left { get; }
    int Top { get; }
    int Width { get; }
    int Height { get; }
    void Activate();
    void Deactivate();
    void Render(float screenX, float screenY);
}

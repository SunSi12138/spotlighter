namespace Spotlighter;

// Defers a single click until Windows' double-click interval has passed.
internal sealed class LampClickGesture
{
    private bool _pending,_ignoreRelease,_pressed;
    internal void Press(){_pressed=true;_ignoreRelease=false;}
    internal bool Release()
    {
        if(!_pressed)return false;
        _pressed=false;
        if(_ignoreRelease){_ignoreRelease=false;return false;}
        _pending=true;return true;
    }
    internal void DoubleClick(){_pending=false;_ignoreRelease=true;_pressed=true;}
    internal bool FireSingle(){bool fire=_pending;_pending=false;return fire;}
}

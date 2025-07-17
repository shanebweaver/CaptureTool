using System.Drawing;

namespace CaptureTool.Capture;

public readonly struct WindowInfo
{
    public nint Handle { get; }
    public string Title { get;  }
    public Rectangle Position { get; }

    public WindowInfo(nint handle, string title, Rectangle position)
    {
        Handle = handle;
        Title = title;
        Position = position;
    }
}

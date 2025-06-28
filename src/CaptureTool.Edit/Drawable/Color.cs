namespace CaptureTool.Edit.Drawable;

public sealed partial class Color
{
    public byte A { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public Color(byte a, byte r, byte g, byte b)
    {
        A = a; R = r; G = g; B = b;
    }
}

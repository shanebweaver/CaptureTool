using System.Numerics;

namespace CaptureTool.Domain.Edit.Abstractions.Drawable;

public partial interface IDrawable
{
    public Vector2 Offset { get; set; }
}

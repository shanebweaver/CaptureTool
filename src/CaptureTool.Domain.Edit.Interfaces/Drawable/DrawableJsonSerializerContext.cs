using System.Text.Json.Serialization;

namespace CaptureTool.Domain.Edit.Interfaces.Drawable;

[JsonSerializable(typeof(RectangleDrawable))]
[JsonSerializable(typeof(EllipseDrawable))]
[JsonSerializable(typeof(LineDrawable))]
[JsonSerializable(typeof(ArrowDrawable))]
[JsonSerializable(typeof(TextDrawable))]
[JsonSerializable(typeof(ImageDrawable))]
public sealed partial class DrawableJsonSerializerContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace CaptureTool.Edit.Image.Win2D.Drawable;

[JsonSerializable(typeof(RectangleDrawable))]
[JsonSerializable(typeof(TextDrawable))]
[JsonSerializable(typeof(ImageDrawable))]
public sealed partial class DrawableJsonSerializerContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace CaptureTool.Edit.Drawable;

[JsonSerializable(typeof(RectangleDrawable))]
[JsonSerializable(typeof(TextDrawable))]
[JsonSerializable(typeof(ImageDrawable))]
public sealed partial class DrawableJsonSerializerContext : JsonSerializerContext
{
}

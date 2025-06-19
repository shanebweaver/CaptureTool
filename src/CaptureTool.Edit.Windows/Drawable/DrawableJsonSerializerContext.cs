using System.Text.Json.Serialization;

namespace CaptureTool.Edit.Windows.Drawable;

[JsonSerializable(typeof(RectangleDrawable))]
[JsonSerializable(typeof(TextDrawable))]
[JsonSerializable(typeof(ImageDrawable))]
public sealed partial class DrawableJsonSerializerContext : JsonSerializerContext
{
}

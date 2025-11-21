using System.Text.Json.Serialization;

namespace CaptureTool.Domains.Edit.Interfaces.Drawable;

[JsonSerializable(typeof(RectangleDrawable))]
[JsonSerializable(typeof(TextDrawable))]
[JsonSerializable(typeof(ImageDrawable))]
public sealed partial class DrawableJsonSerializerContext : JsonSerializerContext
{
}

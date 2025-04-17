using System.Text.Json.Serialization;

namespace CaptureTool.UI.Xaml.Controls.ImageCanvas.Drawable;

[JsonSerializable(typeof(RectangleDrawable))]
[JsonSerializable(typeof(TextDrawable))]
internal sealed partial class DrawableJsonSerializerContext : JsonSerializerContext
{
}

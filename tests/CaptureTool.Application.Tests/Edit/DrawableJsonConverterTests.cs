using CaptureTool.Domain.Edit.Drawable;
using System.Buffers;
using System.Drawing;
using System.Numerics;
using System.Text.Json;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class DrawableJsonConverterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new DrawableJsonConverter() },
    };

    [TestMethod]
    public void Write_WritesDiscriminatorAndTypeValue_ForSupportedDrawables()
    {
        (IDrawable Drawable, int TypeDiscriminator)[] testValues = [
            (new RectangleDrawable(new Vector2(1, 2), new Size(3, 4), Color.Red, Color.Blue, 5), 0),
            (new TextDrawable(new Vector2(1, 2), new Size(3, 4), "hello", Color.Red, Color.Blue, "Arial", 12), 1),
            (new EllipseDrawable(new Vector2(1, 2), new Size(3, 4), Color.Red, Color.Blue, 5), 2),
            (new LineDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 5), 3),
            (new ArrowDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 5), 4),
        ];

        foreach (var (drawable, typeDiscriminator) in testValues)
        {
            string json = Write(drawable);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.AreEqual(typeDiscriminator, document.RootElement.GetProperty("TypeDiscriminator").GetInt32());
            Assert.IsTrue(document.RootElement.TryGetProperty("TypeValue", out JsonElement typeValue));
            Assert.AreEqual(JsonValueKind.Object, typeValue.ValueKind);
        }
    }

    [TestMethod]
    public void Deserialize_Throws_WhenTypeDiscriminatorIsUnknown()
    {
        const string json = """{"TypeDiscriminator":999,"TypeValue":{}}""";

        Assert.ThrowsExactly<JsonException>(() =>
            Read(json));
    }

    [TestMethod]
    public void Read_Throws_WhenJsonDoesNotStartWithObject()
    {
        Assert.ThrowsExactly<JsonException>(() => Read("[]"));
    }

    [TestMethod]
    public void Write_Throws_WhenDrawableTypeIsUnsupported()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => Write(new UnknownDrawable()));
    }

    [TestMethod]
    public void CanConvert_ReturnsTrue_ForDrawableTypes()
    {
        var converter = new DrawableJsonConverter();

        Assert.IsTrue(converter.CanConvert(typeof(IDrawable)));
        Assert.IsTrue(converter.CanConvert(typeof(RectangleDrawable)));
    }

    private static string Write(IDrawable drawable)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            new DrawableJsonConverter().Write(writer, drawable, JsonOptions);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void Read(string json)
    {
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        reader.Read();
        _ = new DrawableJsonConverter().Read(ref reader, typeof(IDrawable), JsonOptions);
    }

    private sealed class UnknownDrawable : IDrawable
    {
        public Vector2 Offset { get; set; }
    }
}

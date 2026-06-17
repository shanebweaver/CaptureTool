using CaptureTool.Application.Features.Settings.Definitions;
using System.Buffers;
using System.Drawing;
using System.Text.Json;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class SettingDefinitionConverterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    [TestMethod]
    public void Write_WritesDiscriminatorAndTypeValue_ForSupportedDefinitions()
    {
        (SettingDefinition Setting, int TypeDiscriminator)[] testValues = [
            (new BoolSettingDefinition("bool", true), 0),
            (new DoubleSettingDefinition("double", 1.5), 1),
            (new IntSettingDefinition("int", 2), 2),
            (new StringSettingDefinition("string", "value"), 3),
            (new PointSettingDefinition("point", new Point(1, 2)), 4),
            (new SizeSettingDefinition("size", new Size(3, 4)), 5),
        ];

        foreach (var (setting, typeDiscriminator) in testValues)
        {
            string json = Write(setting);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.AreEqual(typeDiscriminator, document.RootElement.GetProperty("TypeDiscriminator").GetInt32());
            Assert.IsTrue(document.RootElement.TryGetProperty("TypeValue", out JsonElement typeValue));
            Assert.AreEqual(JsonValueKind.Object, typeValue.ValueKind);
        }
    }

    [TestMethod]
    public void Read_Throws_WhenTypeDiscriminatorIsUnknown()
    {
        Assert.ThrowsExactly<JsonException>(() =>
            Read("""{"TypeDiscriminator":999,"TypeValue":{}}"""));
    }

    [TestMethod]
    public void Read_Throws_WhenJsonDoesNotStartWithObject()
    {
        Assert.ThrowsExactly<JsonException>(() => Read("[]"));
    }

    [TestMethod]
    public void Write_Throws_WhenSettingTypeIsUnsupported()
    {
        Assert.ThrowsExactly<NotSupportedException>(() => Write(new UnknownSettingDefinition("unknown")));
    }

    [TestMethod]
    public void CanConvert_ReturnsTrue_ForSettingDefinitions()
    {
        var converter = new SettingDefinitionConverter();

        Assert.IsTrue(converter.CanConvert(typeof(SettingDefinition)));
        Assert.IsTrue(converter.CanConvert(typeof(BoolSettingDefinition)));
    }

    private static string Write(SettingDefinition setting)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            new SettingDefinitionConverter().Write(writer, setting, JsonOptions);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void Read(string json)
    {
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        reader.Read();
        _ = new SettingDefinitionConverter().Read(ref reader, typeof(SettingDefinition), JsonOptions);
    }

    private sealed class UnknownSettingDefinition(string key) : SettingDefinition(key)
    {
    }
}

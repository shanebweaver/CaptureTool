using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CaptureTool.Domains.Edit.Interfaces.Drawable;

public sealed partial class DrawableJsonConverter : JsonConverter<IDrawable>
{
    private enum TypeDiscriminator
    {
        Rectangle = 0,
        Text,
    }

    public override bool CanConvert(Type typeToConvert) => typeof(IDrawable).IsAssignableFrom(typeToConvert);

    public override IDrawable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        // Get the TypeDescriminator to determine which value type to use.
        TypeDiscriminator typeDiscriminator = ReadTypeDiscriminator(ref reader);

        // Use the TypeDiscriminator to read as the appropriate type.
        IDrawable? drawable = ReadTypeValue(ref reader, typeDiscriminator);

        return !reader.Read() || reader.TokenType != JsonTokenType.EndObject
            ? throw new JsonException()
            : drawable;
    }

    public override void Write(Utf8JsonWriter writer, IDrawable value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case RectangleDrawable rectangleDrawable:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Rectangle, rectangleDrawable, DrawableJsonSerializerContext.Default.RectangleDrawable);
                break;
            case TextDrawable textDrawable:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Text, textDrawable, DrawableJsonSerializerContext.Default.TextDrawable);
                break;
            default:
                throw new NotSupportedException();
        }
    }

    private static TypeDiscriminator ReadTypeDiscriminator(ref Utf8JsonReader reader) =>
        !reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != nameof(TypeDiscriminator) ||
        !reader.Read() || reader.TokenType != JsonTokenType.Number
            ? throw new JsonException()
            : (TypeDiscriminator)reader.GetInt32();

    private static IDrawable? ReadTypeValue(ref Utf8JsonReader reader, TypeDiscriminator typeDiscriminator) =>
        !reader.Read() || reader.GetString() != "TypeValue" ||
        !reader.Read() || reader.TokenType != JsonTokenType.StartObject
            ? throw new JsonException()
            : typeDiscriminator switch
            {
                TypeDiscriminator.Rectangle => JsonSerializer.Deserialize(ref reader, DrawableJsonSerializerContext.Default.RectangleDrawable),
                TypeDiscriminator.Text => JsonSerializer.Deserialize(ref reader, DrawableJsonSerializerContext.Default.TextDrawable),
                _ => throw new JsonException("Unknown TypeDiscriminator value")
            };

    private static void WriteSettingDefinition<T>(ref Utf8JsonWriter writer, TypeDiscriminator typeDiscriminator, T value, JsonTypeInfo<T> typeInfo)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(TypeDiscriminator), (int)typeDiscriminator);
        writer.WritePropertyName("TypeValue");
        JsonSerializer.Serialize(writer, value, typeInfo);
        writer.WriteEndObject();
    }
}
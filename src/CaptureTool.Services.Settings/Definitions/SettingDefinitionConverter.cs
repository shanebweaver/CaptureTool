namespace CaptureTool.Services.Settings.Definitions;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

public class SettingDefinitionConverter : JsonConverter<SettingDefinition>
{
    private enum TypeDiscriminator
    {
        Bool = 0,
        Double,
        Int,
        Point,
        Size,
        String,
        StringArray,
    }

    public override bool CanConvert(Type typeToConvert) => typeof(SettingDefinition).IsAssignableFrom(typeToConvert);

    public override SettingDefinition? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        // Get the TypeDescriminator to determine which value type to use.
        TypeDiscriminator typeDiscriminator = ReadTypeDiscriminator(ref reader);

        // Use the TypeDiscriminator to read as the appropriate type.
        SettingDefinition? settingDefinition = ReadTypeValue(ref reader, typeDiscriminator);

        return !reader.Read() || reader.TokenType != JsonTokenType.EndObject
            ? throw new JsonException()
            : settingDefinition;
    }

    public override void Write(Utf8JsonWriter writer, SettingDefinition value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case BoolSettingDefinition boolSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Bool, boolSettingDefinition, SettingDefinitionContext.Default.BoolSettingDefinition);
                break;
            case DoubleSettingDefinition doubleSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Double, doubleSettingDefinition, SettingDefinitionContext.Default.DoubleSettingDefinition);
                break;
            case IntSettingDefinition intSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Int, intSettingDefinition, SettingDefinitionContext.Default.IntSettingDefinition);
                break;
            case PointSettingDefinition pointSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Point, pointSettingDefinition, SettingDefinitionContext.Default.PointSettingDefinition);
                break;
            case SizeSettingDefinition sizeSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.Size, sizeSettingDefinition, SettingDefinitionContext.Default.SizeSettingDefinition);
                break;
            case StringSettingDefinition stringSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.String, stringSettingDefinition, SettingDefinitionContext.Default.StringSettingDefinition);
                break;
            case StringArraySettingDefinition stringListSettingDefinition:
                WriteSettingDefinition(ref writer, TypeDiscriminator.StringArray, stringListSettingDefinition, SettingDefinitionContext.Default.StringArraySettingDefinition);
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

    private static SettingDefinition? ReadTypeValue(ref Utf8JsonReader reader, TypeDiscriminator typeDiscriminator) =>
        !reader.Read() || reader.GetString() != "TypeValue" ||
        !reader.Read() || reader.TokenType != JsonTokenType.StartObject
            ? throw new JsonException()
            : typeDiscriminator switch
            {
                TypeDiscriminator.Bool => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.BoolSettingDefinition),
                TypeDiscriminator.Double => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.DoubleSettingDefinition),
                TypeDiscriminator.Int => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.IntSettingDefinition),
                TypeDiscriminator.Point => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.PointSettingDefinition),
                TypeDiscriminator.Size => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.SizeSettingDefinition),
                TypeDiscriminator.String => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.StringSettingDefinition),
                TypeDiscriminator.StringArray => JsonSerializer.Deserialize(ref reader, SettingDefinitionContext.Default.StringArraySettingDefinition),
                _ => throw new JsonException("Unknown TypeDiscriminator value")
            };

    private static void WriteSettingDefinition<T>(ref Utf8JsonWriter writer, TypeDiscriminator typeDiscriminator, T settingDefinition, JsonTypeInfo<T> typeInfo)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(TypeDiscriminator), (int)typeDiscriminator);
        writer.WritePropertyName("TypeValue");
        JsonSerializer.Serialize(writer, settingDefinition, typeInfo);
        writer.WriteEndObject();
    }
}

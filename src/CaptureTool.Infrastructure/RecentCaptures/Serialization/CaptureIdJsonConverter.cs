using CaptureTool.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.RecentCaptures.Serialization;

internal sealed class CaptureIdJsonConverter : JsonConverter<CaptureId>
{
    public override CaptureId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("The recent capture identity must be a string.");
        }

        string? value = reader.GetString();
        if (!CaptureId.TryParse(value, out CaptureId captureId))
        {
            throw new JsonException("The recent capture identity is invalid.");
        }

        return captureId;
    }

    public override void Write(
        Utf8JsonWriter writer,
        CaptureId value,
        JsonSerializerOptions options)
    {
        if (value.IsEmpty)
        {
            throw new JsonException("The recent capture identity cannot be empty.");
        }

        writer.WriteStringValue(value.ToString());
    }
}

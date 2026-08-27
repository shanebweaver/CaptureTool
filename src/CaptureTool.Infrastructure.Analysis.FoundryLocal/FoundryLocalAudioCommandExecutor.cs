using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal sealed class FoundryLocalAudioCommandExecutor
{
    private const string NativeLibraryName = "Microsoft.AI.Foundry.Local.Core";
    private const string AudioTranscribeCommand = "audio_transcribe";
    private static readonly nint OnnxRuntimeHandle;
    private static readonly nint OnnxRuntimeGenAiHandle;

    static FoundryLocalAudioCommandExecutor()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Foundry Local requires the regular ORT runtime to load before its GenAI runtime.
        // Holding both handles for the process lifetime also prevents either dependency from
        // being unloaded while the native Foundry singleton is active.
        NativeLibrary.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "onnxruntime.dll"),
            out OnnxRuntimeHandle);
        NativeLibrary.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "onnxruntime-genai.dll"),
            out OnnxRuntimeGenAiHandle);
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.Run(
            () => Execute(
                "initialize",
                CreateParametersJson(
                    ("AppName", "capture-tool"),
                    ("LogLevel", "Warning"))),
            cancellationToken);
    }

    public async Task<string?> FindModelIdAsync(
        string modelAlias,
        string deviceType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceType);

        string catalogJson = await Task.Run(
            () => Execute("get_model_list", requestJson: null),
            cancellationToken).ConfigureAwait(false);
        return FindModelId(catalogJson, modelAlias, deviceType);
    }

    public Task DownloadModelAsync(string modelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return Task.Run(
            () => Execute("download_model", CreateParametersJson(("Model", modelId))),
            cancellationToken);
    }

    public Task LoadModelAsync(string modelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return Task.Run(
            () => Execute("load_model", CreateParametersJson(("Model", modelId))),
            cancellationToken);
    }

    public Task<FoundryLocalAudioTranscription> TranscribeAsync(
        string modelId,
        string audioFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioFilePath);

        return Task.Run(
            () => ReadTranscription(
                Execute(AudioTranscribeCommand, CreateRequestJson(modelId, audioFilePath))),
            cancellationToken);
    }

    internal static string CreateRequestJson(string modelId, string audioFilePath)
    {
        using var openAiRequestStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(openAiRequestStream))
        {
            // Foundry Local 1.x expects the shape produced by its OpenAIAudioClient.
            writer.WriteStartObject();
            writer.WriteString("Model", modelId);
            writer.WriteString("FileName", audioFilePath);
            writer.WriteEndObject();
        }

        string openAiRequest = Encoding.UTF8.GetString(openAiRequestStream.ToArray());
        using var commandStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(commandStream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Params");
            writer.WriteStartObject();
            writer.WriteString("OpenAICreateRequest", openAiRequest);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(commandStream.ToArray());
    }

    internal static string? FindModelId(string catalogJson, string modelAlias, string deviceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogJson);
        using JsonDocument catalog = JsonDocument.Parse(catalogJson);
        foreach (JsonElement model in catalog.RootElement.EnumerateArray())
        {
            if (!TryGetString(model, "alias", out string? alias) ||
                !string.Equals(alias, modelAlias, StringComparison.Ordinal))
            {
                continue;
            }

            if (!model.TryGetProperty("runtime", out JsonElement runtime) ||
                !TryGetString(runtime, "deviceType", out string? device) ||
                !string.Equals(device, deviceType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryGetString(model, "id", out string? id) && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return null;
    }

    internal static string ReadTranscript(string responseJson)
    {
        return ReadTranscription(responseJson).Text;
    }

    internal static FoundryLocalAudioTranscription ReadTranscription(string responseJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        using JsonDocument response = JsonDocument.Parse(responseJson);
        JsonElement root = response.RootElement;
        if (!TryGetString(root, "text", out string? transcript))
        {
            throw new InvalidOperationException(
                "Foundry Local returned an audio response without transcript text.");
        }

        var segments = new List<FoundryLocalAudioTranscriptionSegment>();
        if (TryGetProperty(root, "segments", out JsonElement segmentArray) &&
            segmentArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement segment in segmentArray.EnumerateArray())
            {
                if (segments.Count >= 50_000 ||
                    !TryGetString(segment, "text", out string? text) ||
                    string.IsNullOrWhiteSpace(text) ||
                    !TryGetDouble(segment, "start", out double startSeconds) ||
                    !TryGetDouble(segment, "end", out double endSeconds) ||
                    !double.IsFinite(startSeconds) ||
                    !double.IsFinite(endSeconds) ||
                    startSeconds < 0 ||
                    endSeconds < startSeconds ||
                    endSeconds > TimeSpan.MaxValue.TotalSeconds)
                {
                    continue;
                }

                segments.Add(new FoundryLocalAudioTranscriptionSegment(
                    text,
                    TimeSpan.FromSeconds(startSeconds),
                    TimeSpan.FromSeconds(endSeconds)));
            }
        }

        TryGetString(root, "language", out string? language);
        return new FoundryLocalAudioTranscription(
            transcript ?? string.Empty,
            segments.AsReadOnly(),
            language);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (TryGetProperty(element, propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        if (TryGetProperty(element, propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string CreateParametersJson(params (string Name, string Value)[] parameters)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Params");
            writer.WriteStartObject();
            foreach ((string name, string value) in parameters)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Execute(string command, string? requestJson)
    {
        byte[] commandBytes = Encoding.UTF8.GetBytes(command);
        byte[]? requestBytes = requestJson == null ? null : Encoding.UTF8.GetBytes(requestJson);
        nint commandPointer = Marshal.AllocHGlobal(commandBytes.Length);
        nint requestPointer = requestBytes == null
            ? nint.Zero
            : Marshal.AllocHGlobal(requestBytes.Length);
        ResponseBuffer response = default;
        try
        {
            Marshal.Copy(commandBytes, 0, commandPointer, commandBytes.Length);
            if (requestBytes != null)
            {
                Marshal.Copy(requestBytes, 0, requestPointer, requestBytes.Length);
            }

            var request = new RequestBuffer
            {
                Command = commandPointer,
                CommandLength = commandBytes.Length,
                Data = requestPointer,
                DataLength = requestBytes?.Length ?? 0,
            };

            ExecuteCommand(ref request, ref response);
            if (response.Error != nint.Zero && response.ErrorLength > 0)
            {
                throw new InvalidOperationException(
                    Marshal.PtrToStringUTF8(response.Error, response.ErrorLength));
            }

            if (response.Data == nint.Zero || response.DataLength <= 0)
            {
                throw new InvalidOperationException("Foundry Local returned an empty audio response.");
            }

            string responseJson = Marshal.PtrToStringUTF8(response.Data, response.DataLength)
                ?? throw new InvalidOperationException("Foundry Local returned invalid audio response data.");
            return responseJson;
        }
        finally
        {
            Marshal.FreeHGlobal(commandPointer);
            if (requestPointer != nint.Zero)
            {
                Marshal.FreeHGlobal(requestPointer);
            }
            Marshal.FreeHGlobal(response.Data);
            Marshal.FreeHGlobal(response.Error);
        }
    }

    [DllImport(NativeLibraryName, EntryPoint = "execute_command", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ExecuteCommand(ref RequestBuffer request, ref ResponseBuffer response);

    [StructLayout(LayoutKind.Sequential)]
    private struct RequestBuffer
    {
        public nint Command;
        public int CommandLength;
        public nint Data;
        public int DataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ResponseBuffer
    {
        public nint Data;
        public int DataLength;
        public nint Error;
        public int ErrorLength;
    }
}

internal sealed record FoundryLocalAudioTranscription(
    string Text,
    IReadOnlyList<FoundryLocalAudioTranscriptionSegment> Segments,
    string? Language);

internal sealed record FoundryLocalAudioTranscriptionSegment(
    string Text,
    TimeSpan StartTime,
    TimeSpan EndTime);

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal;

internal enum FoundryLocalSpeechTranscriptionMode
{
    File,
    LivePcm,
}

internal sealed record FoundryLocalSpeechModelConfiguration(
    string AnalyzerId,
    string ModelAlias,
    string AdapterVersion,
    string SelectionPolicyRevision,
    string DefaultLanguageHint,
    FoundryLocalModelDevicePreference DevicePreference,
    FoundryLocalSpeechTranscriptionMode TranscriptionMode,
    int QualityTier,
    bool FallbackOnFailure)
{
    public const string RuntimeVersion = "1.2.4";

    public static readonly TimeSpan MaximumTimestampWindow = TimeSpan.FromSeconds(15);

    public static FoundryLocalSpeechModelConfiguration Whisper { get; } = new(
        AnalyzerId: "foundry-local-speech-transcript",
        ModelAlias: "whisper-tiny",
        AdapterVersion: "2.2.0",
        SelectionPolicyRevision: "alias-cpu-pcm16-app-language-allowlist-v4",
        DefaultLanguageHint: "en",
        DevicePreference: FoundryLocalModelDevicePreference.Cpu,
        TranscriptionMode: FoundryLocalSpeechTranscriptionMode.File,
        QualityTier: 40,
        FallbackOnFailure: false);

    public static FoundryLocalSpeechModelConfiguration NemotronMultilingual { get; } = new(
        AnalyzerId: "foundry-local-nemotron-multilingual-speech-transcript",
        ModelAlias: "nvidia-nemotron-3.5-asr-streaming-multilingual-0.6b",
        AdapterVersion: "1.1.0",
        SelectionPolicyRevision: "alias-cpu-live-pcm16-language-auto-v2",
        DefaultLanguageHint: "auto",
        DevicePreference: FoundryLocalModelDevicePreference.Cpu,
        TranscriptionMode: FoundryLocalSpeechTranscriptionMode.LivePcm,
        QualityTier: 50,
        FallbackOnFailure: true);

}

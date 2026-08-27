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
    FoundryLocalSpeechTranscriptionMode TranscriptionMode,
    int QualityTier,
    bool FallbackOnFailure)
{
    public const string RuntimeVersion = "1.2.4";

    public static readonly TimeSpan MaximumTimestampWindow = TimeSpan.FromSeconds(15);

    public static FoundryLocalSpeechModelConfiguration Whisper { get; } = new(
        AnalyzerId: "foundry-local-speech-transcript",
        ModelAlias: "whisper-tiny",
        AdapterVersion: "2.1.0",
        SelectionPolicyRevision: "alias-auto-winml-pcm16-app-language-allowlist-v3",
        DefaultLanguageHint: "en",
        TranscriptionMode: FoundryLocalSpeechTranscriptionMode.File,
        QualityTier: 40,
        FallbackOnFailure: false);

    public static FoundryLocalSpeechModelConfiguration NemotronMultilingual { get; } = new(
        AnalyzerId: "foundry-local-nemotron-multilingual-speech-transcript",
        ModelAlias: "nvidia-nemotron-3.5-asr-streaming-multilingual-0.6b",
        AdapterVersion: "1.0.0",
        SelectionPolicyRevision: "alias-auto-winml-live-pcm16-language-auto-v1",
        DefaultLanguageHint: "auto",
        TranscriptionMode: FoundryLocalSpeechTranscriptionMode.LivePcm,
        QualityTier: 50,
        FallbackOnFailure: true);

}

using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Security.Cryptography;
using Windows.Media.Editing;
using Windows.Storage;

internal static class AudioChecks
{
    public static async Task<IReadOnlyList<SmokeResult>> RunAsync(IMediaAnalyzer[] analyzers, string output, string scratch)
    {
        string silent = Path.Combine(output, "synthetic-silence.wav");
        using (var writer = new BinaryWriter(File.Create(silent)))
        {
            const int bytes = 16000 * 2 * 2;
            writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(16000);
            writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
        List<SmokeResult> results = [];
        foreach (var analyzer in analyzers.Where(item => item.Descriptor.Capability == AnalysisCapability.Transcription))
        foreach (var fixture in new[] {
            (File: "synthetic-silence.wav", Kind: AnalysisMediaKind.Audio, Language: "en", Word: (string?)null),
            (File: "synthetic-shapes.mp4", Kind: AnalysisMediaKind.Video, Language: "en", Word: (string?)null),
            (File: "synthetic-de.wav", Kind: AnalysisMediaKind.Audio, Language: "de", Word: (string?)"Sonne"),
            (File: "synthetic-fr.wav", Kind: AnalysisMediaKind.Audio, Language: "fr", Word: (string?)"soleil") })
        {
            string label = analyzer.Descriptor.Id + "/" + Path.GetFileNameWithoutExtension(fixture.File);
            string path = Path.Combine(output, fixture.File);
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                var ready = await analyzer.GetAvailabilityAsync(fixture.Kind, fixture.Language, budget.Token);
                if (ready != AnalyzerAvailability.Ready || !File.Exists(path))
                {
                    results.Add(new(label, fixture.Kind.ToString(), File.Exists(path) ? ready.ToString() : "MissingSyntheticVoice", null, null, null));
                    continue;
                }
                var source = await StorageFile.GetFileFromPathAsync(path);
                TimeSpan duration = fixture.Kind == AnalysisMediaKind.Audio
                    ? (await BackgroundAudioTrack.CreateFromFileAsync(source)).OriginalDuration
                    : (await MediaClip.CreateFromFileAsync(source)).OriginalDuration;
                byte[] before = await File.ReadAllBytesAsync(path, budget.Token);
                var revision = new SourceRevision(Convert.ToHexStringLower(SHA256.HashData(before)));
                var outcome = await analyzer.AnalyzeAsync(new(CaptureId.New(), fixture.Kind, revision, path, fixture.Language), null, budget.Token);
                bool valid = outcome.Kind == AnalyzerOutcomeKind.Succeeded && outcome.Payload is TranscriptMetadata && outcome.Producer != null;
                var transcript = outcome.Payload as TranscriptMetadata;
                string? text = null;
                if (transcript != null)
                {
                    text = string.Join(" ", transcript.Segments.Select(segment => segment.Text));
                    valid &= fixture.Word == null ? transcript.Segments.Count == 0 : text.Contains(fixture.Word, StringComparison.OrdinalIgnoreCase);
                    valid &= transcript.Segments.All(segment => segment.Start >= TimeSpan.Zero && segment.End <= duration + TimeSpan.FromMilliseconds(100));
                }
                byte[] after = await File.ReadAllBytesAsync(path, budget.Token);
                valid &= before.SequenceEqual(after);
                valid &= !Directory.Exists(scratch) || !Directory.EnumerateFiles(scratch, "*.wav", SearchOption.AllDirectories).Any();
                results.Add(new(label, fixture.Kind.ToString(), valid ? "Succeeded" : "FixtureMismatch", transcript?.Segments.Count,
                    outcome.Producer?.ModelId, outcome.FailureCode, SyntheticTranscript: text));
                Console.WriteLine($"{label}: {results[^1].Status}");
            }
            catch (Exception exception)
            {
                results.Add(new(label, fixture.Kind.ToString(), "Error:" + exception.GetType().Name, null, null, exception.ToString()));
            }
        }
        return results;
    }
}

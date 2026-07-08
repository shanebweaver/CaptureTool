namespace CaptureTool.Presentation.Features.Audio;

public interface IAudioWaveformHistory
{
    void Save(string audioPath, IReadOnlyList<double> levels);

    IReadOnlyList<double>? TryGet(string audioPath);
}

namespace CaptureTool.Presentation.Features.Audio;

internal sealed class AudioWaveformHistory : IAudioWaveformHistory
{
    private const int MaxHistoryEntries = 16;
    private readonly Dictionary<string, double[]> _levelsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _paths = [];
    private readonly Lock _syncRoot = new();

    public void Save(string audioPath, IReadOnlyList<double> levels)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
        {
            return;
        }

        lock (_syncRoot)
        {
            if (!_levelsByPath.ContainsKey(audioPath))
            {
                _paths.Enqueue(audioPath);
            }

            _levelsByPath[audioPath] = levels.ToArray();
            TrimHistory();
        }
    }

    public IReadOnlyList<double>? TryGet(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _levelsByPath.TryGetValue(audioPath, out double[]? levels)
                ? levels
                : null;
        }
    }

    private void TrimHistory()
    {
        while (_paths.Count > MaxHistoryEntries)
        {
            string path = _paths.Dequeue();
            _levelsByPath.Remove(path);
        }
    }
}

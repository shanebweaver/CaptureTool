using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Edit.Audio.CopyAudioFile;
using CaptureTool.Application.Abstractions.Edit.Audio.SaveAudioFile;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.Audio;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.Features.AudioEdit;

public sealed partial class AudioEditPageViewModel : LoadableViewModelBase<AudioFile>
{
    private const int WaveformBarCount = 64;
    private const double WaveformMinBarHeight = 0;
    private const double WaveformMaxBarHeight = 132;

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }
    public IRelayCommand NewAudioCaptureCommand { get; }

    public string? AudioPath
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsAudioReady
    {
        get;
        private set => Set(ref field, value);
    }

    private readonly ISaveAudioFileUseCase _saveAction;
    private readonly ICopyAudioFileUseCase _copyAction;
    private readonly IAudioWaveformHistory _waveformHistory;

    public ObservableCollection<AudioWaveformBarViewModel> WaveformBars { get; }

    public AudioEditPageViewModel(
        ISaveAudioFileUseCase saveAction,
        ICopyAudioFileUseCase copyAction,
        IOpenAudioCapturePageUseCase openAudioCapturePageAction,
        IAudioWaveformHistory waveformHistory)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _waveformHistory = waveformHistory;

        SaveCommand = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NewAudioCaptureCommand = openAudioCapturePageAction.ToRelayCommand(() => new OpenAudioCapturePageRequest());

        IsAudioReady = false;
        WaveformBars = [];
        ResetWaveform();
    }

    public override void Load(AudioFile audio)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        AudioPath = audio.FilePath;
        IsAudioReady = true;
        ResetWaveform();

        base.Load(audio);
    }

    public IReadOnlyList<double>? GetCapturedWaveformLevels(string audioPath)
        => _waveformHistory.TryGet(audioPath);

    public void SetWaveformLevels(IReadOnlyList<double> levels)
    {
        for (int index = 0; index < WaveformBarCount; index++)
        {
            double level = index < levels.Count ? levels[index] : 0;
            double height = GetWaveformBarHeight(level);

            if (index < WaveformBars.Count)
            {
                WaveformBars[index].Height = height;
            }
            else
            {
                WaveformBars.Add(new AudioWaveformBarViewModel(height));
            }
        }

        while (WaveformBars.Count > WaveformBarCount)
        {
            WaveformBars.RemoveAt(WaveformBars.Count - 1);
        }
    }

    private static double GetWaveformBarHeight(double level)
    {
        double clampedLevel = Math.Clamp(level, 0, 1);
        return WaveformMinBarHeight + (clampedLevel * (WaveformMaxBarHeight - WaveformMinBarHeight));
    }

    private void ResetWaveform()
    {
        WaveformBars.Clear();
        for (int i = 0; i < WaveformBarCount; i++)
        {
            WaveformBars.Add(new AudioWaveformBarViewModel(WaveformMinBarHeight));
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            return;
        }

        await _saveAction.ExecuteAsync(new SaveAudioFileRequest(AudioPath), CancellationToken.None);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            return;
        }

        await _copyAction.ExecuteAsync(new CopyAudioFileRequest(AudioPath), CancellationToken.None);
    }
}

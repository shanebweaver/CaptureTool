using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class AudioEditPageViewModel : LoadableViewModelBase<IAudioFile>
{
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand CopyCommand { get; }

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

    private readonly ISaveAudioFileAppCommand _saveAction;
    private readonly ICopyAudioFileAppCommand _copyAction;
    private readonly ITelemetryService _telemetryService;

    public AudioEditPageViewModel(
        ISaveAudioFileAppCommand saveAction,
        ICopyAudioFileAppCommand copyAction,
        ITelemetryService telemetryService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _telemetryService = telemetryService;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CopyCommand = new AsyncRelayCommand(CopyAsync);

        IsAudioReady = false;
    }

    public override void Load(IAudioFile audio)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        AudioPath = audio.FilePath;
        IsAudioReady = true;

        base.Load(audio);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            throw new InvalidOperationException("Cannot save audio without a valid filepath.");
        }

        await _saveAction.ExecuteAsync(AudioPath, CancellationToken.None);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            throw new InvalidOperationException("Cannot copy audio to clipboard without a valid filepath.");
        }

        await _copyAction.ExecuteAsync(AudioPath, CancellationToken.None);
    }
}

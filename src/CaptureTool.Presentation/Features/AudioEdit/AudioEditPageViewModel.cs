using CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.AudioEdit;

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

    private readonly ISaveAudioFileUseCase _saveAction;
    private readonly ICopyAudioFileUseCase _copyAction;
    private readonly ITelemetryService _telemetryService;

    public AudioEditPageViewModel(
        ISaveAudioFileUseCase saveAction,
        ICopyAudioFileUseCase copyAction,
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
        try
        {
            if (string.IsNullOrEmpty(AudioPath))
            {
                throw new InvalidOperationException("Cannot save audio without a valid filepath.");
            }

            await _saveAction.ExecuteAsync(new SaveAudioFileRequest(AudioPath), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(SaveAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(SaveAsync), exception);
        }
    }

    private async Task CopyAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(AudioPath))
            {
                throw new InvalidOperationException("Cannot copy audio to clipboard without a valid filepath.");
            }

            await _copyAction.ExecuteAsync(new CopyAudioFileRequest(AudioPath), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(nameof(CopyAsync), exception.Message);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(nameof(CopyAsync), exception);
        }
    }
}

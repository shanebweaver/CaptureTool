using CaptureTool.Presentation.ViewModels.Helpers;
using CaptureTool.Application.Abstractions.UseCases.AudioEdit;
using CaptureTool.Infrastructure.UseCases.Extensions;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Telemetry;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class AudioEditPageViewModel : LoadableViewModelBase<IAudioFile>
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = $"LoadAudioEditPage";
        public static readonly string Save = $"Save";
        public static readonly string Copy = $"Copy";
    }

    private const string TelemetryContext = "AudioEditPage";

    public IAsyncAppCommand SaveCommand { get; }
    public IAsyncAppCommand CopyCommand { get; }

    public string? AudioPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsAudioReady
    {
        get => field;
        private set => Set(ref field, value);
    }

    private readonly IAudioEditSaveUseCase _saveAction;
    private readonly IAudioEditCopyUseCase _copyAction;
    private readonly ITelemetryService _telemetryService;

    public AudioEditPageViewModel(
        IAudioEditSaveUseCase saveAction,
        IAudioEditCopyUseCase copyAction,
        ITelemetryService telemetryService)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;
        _telemetryService = telemetryService;

        TelemetryAppCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        SaveCommand = commandFactory.CreateAsync(ActivityIds.Save, SaveAsync);
        CopyCommand = commandFactory.CreateAsync(ActivityIds.Copy, CopyAsync);

        IsAudioReady = false;
    }

    public override void Load(IAudioFile audio)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, TelemetryContext, ActivityIds.Load, () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            AudioPath = audio.FilePath;
            IsAudioReady = true;

            base.Load(audio);
        });
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            throw new InvalidOperationException("Cannot save audio without a valid filepath.");
        }

        await _saveAction.ExecuteCommandAsync(AudioPath, CancellationToken.None);
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(AudioPath))
        {
            throw new InvalidOperationException("Cannot copy audio to clipboard without a valid filepath.");
        }

        await _copyAction.ExecuteCommandAsync(AudioPath, CancellationToken.None);
    }
}

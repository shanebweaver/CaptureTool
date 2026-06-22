using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Features.AudioEdit.CopyAudioFile;
using CaptureTool.Application.Abstractions.Features.AudioEdit.SaveAudioFile;
using CaptureTool.Domain.Capture.Files;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;

namespace CaptureTool.Presentation.Features.AudioEdit;

public sealed partial class AudioEditPageViewModel : LoadableViewModelBase<IAudioFile>
{
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

    public AudioEditPageViewModel(
        ISaveAudioFileUseCase saveAction,
        ICopyAudioFileUseCase copyAction,
        IOpenAudioCapturePageUseCase openAudioCapturePageAction)
    {
        _saveAction = saveAction;
        _copyAction = copyAction;

        SaveCommand = new AsyncRelayCommand(SaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        CopyCommand = new AsyncRelayCommand(CopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        NewAudioCaptureCommand = openAudioCapturePageAction.ToRelayCommand(() => new OpenAudioCapturePageRequest());

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

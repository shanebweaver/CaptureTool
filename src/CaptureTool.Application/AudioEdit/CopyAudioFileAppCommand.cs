using CaptureTool.Application.Abstractions.AudioEdit;
using CaptureTool.Infrastructure.Abstractions.Clipboard;

namespace CaptureTool.Application.AudioEdit;

internal class CopyAudioFileAppCommand : ICopyAudioFileAppCommand
{
    private readonly IClipboardService _clipboardService;

    public CopyAudioFileAppCommand(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public bool IsExecuting { get; protected set; }

    public bool CanExecute(string parameter)
    {
        return !string.IsNullOrEmpty(parameter) && File.Exists(parameter);
    }

    public async Task ExecuteAsync(string audioPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(audioPath))
        {
            throw new InvalidOperationException("Cannot copy audio to clipboard without a valid filepath.");
        }

        ClipboardFile clipboardAudio = new(audioPath);
        await _clipboardService.CopyFileAsync(clipboardAudio);
    }
}

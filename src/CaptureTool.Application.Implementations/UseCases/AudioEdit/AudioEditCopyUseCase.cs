using CaptureTool.Application.Interfaces.UseCases.AudioEdit;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Clipboard;

namespace CaptureTool.Application.Implementations.UseCases.AudioEdit;

public sealed partial class AudioEditCopyUseCase : AsyncUseCase<string>, IAudioEditCopyUseCase
{
    private readonly IClipboardService _clipboardService;

    public AudioEditCopyUseCase(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public override async Task ExecuteAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(audioPath))
        {
            throw new InvalidOperationException("Cannot copy audio to clipboard without a valid filepath.");
        }

        ClipboardFile clipboardAudio = new(audioPath);
        await _clipboardService.CopyFileAsync(clipboardAudio);
    }
}

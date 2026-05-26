using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Settings;

internal class SettingsUpdateVideoCaptureDefaultLocalAudioAppCommand : ISettingsUpdateVideoCaptureDefaultLocalAudioAppCommand
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureDefaultLocalAudioAppCommand(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(bool parameter)
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        IsExecuting = true;

        try
        {
            _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled, parameter);
            await _settingsService.TrySaveAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }
}

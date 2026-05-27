using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.UseCases.Settings;

internal class SettingsUpdateVideoCaptureAutoSaveAppCommand : ISettingsUpdateVideoCaptureAutoSaveAppCommand
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureAutoSaveAppCommand(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsExecuting { get; protected set; }

    public bool CanExecute(bool parameter)
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        IsExecuting = true;
        try
        {
            _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, parameter);
            await _settingsService.TrySaveAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }
}

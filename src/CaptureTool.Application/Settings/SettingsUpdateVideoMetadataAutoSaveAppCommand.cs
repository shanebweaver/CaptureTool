using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Settings;

internal class SettingsUpdateVideoMetadataAutoSaveAppCommand : ISettingsUpdateVideoMetadataAutoSaveAppCommand
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoMetadataAutoSaveAppCommand(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool IsExecuting { get; protected set; }

    public bool CanExecute(bool parameter)
    {
        throw new NotImplementedException();
    }

    public async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        IsExecuting = true;

        try
        {
            _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave, parameter);
            await _settingsService.TrySaveAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }
}

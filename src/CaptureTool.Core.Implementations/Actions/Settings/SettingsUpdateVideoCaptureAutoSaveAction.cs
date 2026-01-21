using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateVideoCaptureAutoSaveAction : AsyncActionCommand<bool>, ISettingsUpdateVideoCaptureAutoSaveAction
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureAutoSaveAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoSave, parameter);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}

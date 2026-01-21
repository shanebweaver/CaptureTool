using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateVideoCaptureAutoCopyAction : AsyncActionCommand<bool>, ISettingsUpdateVideoCaptureAutoCopyAction
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureAutoCopyAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(bool parameter)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, parameter);
        await _settingsService.TrySaveAsync(CancellationToken.None);
    }
}

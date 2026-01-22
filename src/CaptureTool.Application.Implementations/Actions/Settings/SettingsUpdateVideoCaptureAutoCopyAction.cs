using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateVideoCaptureAutoCopyAction : AsyncActionCommand<bool>, ISettingsUpdateVideoCaptureAutoCopyAction
{
    private readonly ISettingsService _settingsService;

    public SettingsUpdateVideoCaptureAutoCopyAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_AutoCopy, parameter);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}

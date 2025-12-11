using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateImageAutoCopyAction : AsyncActionCommand<bool>, ISettingsUpdateImageAutoCopyAction
{
    private readonly ISettingsService _settingsService;
    public SettingsUpdateImageAutoCopyAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    public override async Task ExecuteAsync(bool parameter)
    {
        _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, parameter);
        await _settingsService.TrySaveAsync(CancellationToken.None);
    }
}

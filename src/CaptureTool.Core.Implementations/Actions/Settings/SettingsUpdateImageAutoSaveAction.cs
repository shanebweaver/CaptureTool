using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsUpdateImageAutoSaveAction : AsyncActionCommand<bool>, ISettingsUpdateImageAutoSaveAction
{
    private readonly ISettingsService _settingsService;
    public SettingsUpdateImageAutoSaveAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }
    public override async Task ExecuteAsync(bool parameter)
    {
        _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, parameter);
        await _settingsService.TrySaveAsync(CancellationToken.None);
    }
}

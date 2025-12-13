using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Settings;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsRestoreDefaultsAction : AsyncActionCommand, ISettingsRestoreDefaultsAction
{
    private readonly ISettingsService _settingsService;

    public SettingsRestoreDefaultsAction(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _settingsService.ClearAllSettings();
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}

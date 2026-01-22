using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.Actions.Settings;

public sealed partial class SettingsRestoreDefaultsAction : AsyncActionCommand, ISettingsRestoreDefaultsAction
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    public SettingsRestoreDefaultsAction(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
    }

    public override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _settingsService.ClearAllSettings();
        _localizationService.OverrideLanguage(null);
        await _settingsService.TrySaveAsync(cancellationToken);
    }
}

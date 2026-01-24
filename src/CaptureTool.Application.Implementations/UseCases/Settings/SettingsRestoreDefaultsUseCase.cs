using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Settings;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsRestoreDefaultsUseCase : AsyncUseCase, ISettingsRestoreDefaultsUseCase
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    public SettingsRestoreDefaultsUseCase(ISettingsService settingsService, ILocalizationService localizationService)
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

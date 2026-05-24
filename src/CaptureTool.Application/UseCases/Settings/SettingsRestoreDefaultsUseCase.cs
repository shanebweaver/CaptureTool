using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.UseCases.Settings;

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

using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Settings;

internal class SettingsRestoreDefaultsAppCommand : ISettingsRestoreDefaultsAppCommand
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    public SettingsRestoreDefaultsAppCommand(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
    }

    public bool IsExecuting { get; protected set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return !IsExecuting;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IsExecuting = true;

        try
        {
            _settingsService.ClearAllSettings();
            _localizationService.OverrideLanguage(null);
            await _settingsService.TrySaveAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }
}

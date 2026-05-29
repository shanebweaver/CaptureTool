using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.RestoreDefaults;

public sealed class RestoreDefaultsUseCase : IUseCase<RestoreDefaultsRequest, RestoreDefaultsResponse>, IConditional<RestoreDefaultsRequest>
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    public RestoreDefaultsUseCase(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
    }

    public Task<bool> CanExecuteAsync(RestoreDefaultsRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<RestoreDefaultsResponse> ExecuteAsync(RestoreDefaultsRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.ClearAllSettings();
        _localizationService.OverrideLanguage(null);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new RestoreDefaultsResponse();
    }
}
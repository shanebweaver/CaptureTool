using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateAppLanguage;

public sealed class UpdateAppLanguageUseCase : IUseCase<UpdateAppLanguageRequest, UpdateAppLanguageResponse>, IConditional<UpdateAppLanguageRequest>
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;

    public UpdateAppLanguageUseCase(ILocalizationService localization, ISettingsService settings)
    {
        _localization = localization;
        _settings = settings;
    }

    public Task<bool> CanExecuteAsync(UpdateAppLanguageRequest request, CancellationToken cancellationToken = default)
    {
        var languages = _localization.SupportedLanguages;
        return Task.FromResult(request.LanguageIndex >= 0 && request.LanguageIndex <= languages.Length);
    }

    public async Task<UpdateAppLanguageResponse> ExecuteAsync(UpdateAppLanguageRequest request, CancellationToken cancellationToken = default)
    {
        var languages = _localization.SupportedLanguages;
        if (request.LanguageIndex < 0 || request.LanguageIndex > languages.Length)
        {
            return new UpdateAppLanguageResponse();
        }

        var language = request.LanguageIndex == languages.Length ? null : languages[request.LanguageIndex];
        _localization.OverrideLanguage(language);

        if (language?.Value is string value)
        {
            _settings.Set(CaptureToolSettings.Settings_LanguageOverride, value);
        }
        else
        {
            _settings.Unset(CaptureToolSettings.Settings_LanguageOverride);
        }

        await _settings.TrySaveAsync(cancellationToken);
        return new UpdateAppLanguageResponse();
    }
}
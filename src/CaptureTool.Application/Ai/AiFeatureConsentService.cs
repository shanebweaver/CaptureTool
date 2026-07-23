using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Domain.Ai;

namespace CaptureTool.Application.Ai;

internal sealed class AiFeatureConsentService : IAiFeatureConsentService
{
    private readonly ISettingsService _settingsService;

    public AiFeatureConsentService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<AiFeatureConsent> GetFeatureConsents()
    {
        return [
            CreateConsent(AiFeatureId.TextExtraction, "Text extraction"),
            CreateConsent(AiFeatureId.ImageSuperResolution, "Super image resolution"),
            CreateConsent(AiFeatureId.ImageDescription, "Image description"),
            CreateConsent(AiFeatureId.ImageForegroundExtraction, "Background removal")
        ];
    }

    public AiFeatureConsentState GetConsentState(AiFeatureId featureId)
    {
        IBoolSettingDefinition settingDefinition = GetSettingDefinition(featureId);
        if (!_settingsService.IsSet(settingDefinition))
        {
            return AiFeatureConsentState.Unknown;
        }

        return _settingsService.Get(settingDefinition)
            ? AiFeatureConsentState.Granted
            : AiFeatureConsentState.Denied;
    }

    public async Task SetConsentAsync(
        AiFeatureId featureId,
        bool isGranted,
        CancellationToken cancellationToken = default)
    {
        _settingsService.Set(GetSettingDefinition(featureId), isGranted);
        await _settingsService.TrySaveAsync(cancellationToken);
    }

    private AiFeatureConsent CreateConsent(AiFeatureId featureId, string displayName)
    {
        return new(featureId, displayName, GetConsentState(featureId));
    }

    private static IBoolSettingDefinition GetSettingDefinition(AiFeatureId featureId)
    {
        return featureId switch
        {
            AiFeatureId.TextExtraction => CaptureToolSettings.Settings_AiConsent_TextExtraction,
            AiFeatureId.ImageSuperResolution => CaptureToolSettings.Settings_AiConsent_ImageSuperResolution,
            AiFeatureId.ImageDescription => CaptureToolSettings.Settings_AiConsent_ImageDescription,
            AiFeatureId.ImageForegroundExtraction => CaptureToolSettings.Settings_AiConsent_ImageForegroundExtraction,
            _ => throw new ArgumentOutOfRangeException(nameof(featureId), featureId, null)
        };
    }
}


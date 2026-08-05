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
            CreateConsent(AiFeatureId.ImageForegroundExtraction, "Background removal"),
            CreateConsent(AiFeatureId.ImageObjectErase, "Object erase"),
            CreateConsent(AiFeatureId.ImageObjectExtraction, "Object extraction"),
            CreateConsent(AiFeatureId.VideoSuperResolution, "Video super resolution")
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

    public async Task<bool> SetConsentAsync(
        AiFeatureId featureId,
        bool isGranted,
        CancellationToken cancellationToken = default)
    {
        SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
            GetSettingDefinition(featureId),
            isGranted,
            cancellationToken);
        return result.Succeeded;
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
            AiFeatureId.ImageObjectErase => CaptureToolSettings.Settings_AiConsent_ImageObjectErase,
            AiFeatureId.ImageObjectExtraction => CaptureToolSettings.Settings_AiConsent_ImageObjectExtraction,
            AiFeatureId.VideoSuperResolution => CaptureToolSettings.Settings_AiConsent_VideoSuperResolution,
            _ => throw new ArgumentOutOfRangeException(nameof(featureId), featureId, null)
        };
    }
}


namespace CaptureTool.Application.Abstractions.Features;

public interface IFeatureAvailabilityService
{
    bool IsAddOnsStoreEnabled { get; }

    bool IsAudioCaptureEnabled { get; }

    bool IsAudioInputSelectionEnabled { get; }

    bool IsImageEditChromaKeyEnabled { get; }
}

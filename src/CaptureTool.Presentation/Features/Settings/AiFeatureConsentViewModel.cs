using CaptureTool.Domain.Ai;
using CaptureTool.Presentation.ViewModels;

namespace CaptureTool.Presentation.Features.Settings;

public sealed partial class AiFeatureConsentViewModel : ViewModelBase
{
    public AiFeatureConsentViewModel(
        AiFeatureId featureId,
        string displayName,
        bool isConsented)
    {
        FeatureId = featureId;
        DisplayName = displayName;
        IsConsented = isConsented;
    }

    public AiFeatureId FeatureId { get; }

    public string DisplayName { get; }

    public bool IsConsented
    {
        get;
        private set => Set(ref field, value);
    }

    public void ApplyConsent(bool isConsented)
    {
        IsConsented = isConsented;
    }
}


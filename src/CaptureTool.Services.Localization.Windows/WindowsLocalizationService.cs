using Windows.ApplicationModel.Resources;

namespace CaptureTool.Services.Localization.Windows;

public sealed partial class WindowsLocalizationService : ILocalizationService
{
    private readonly ResourceLoader _resourceLoader;
    public WindowsLocalizationService()
    {
        _resourceLoader = new ResourceLoader();
    }

    public string GetString(string resourceKey)
    {
        return _resourceLoader.GetString(resourceKey);
    }
}

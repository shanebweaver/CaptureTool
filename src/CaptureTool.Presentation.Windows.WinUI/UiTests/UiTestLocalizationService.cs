using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.Windows.ApplicationModel.Resources;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestLocalizationService : ILocalizationService
{
    private readonly IAppLanguage _defaultLanguage = new UiTestAppLanguage(UiTestLaunchOptions.Current.Language ?? "en-US");
    private bool _isInitialized;
    private ResourceLoader? _resources;

    public IAppLanguage? LanguageOverride { get; private set; }
    public IAppLanguage? RequestedLanguage { get; private set; }
    public IAppLanguage? StartupLanguage { get; private set; }
    public IAppLanguage? DefaultLanguage => _defaultLanguage;
    public IAppLanguage[] SupportedLanguages { get; }

    public UiTestLocalizationService()
    {
        SupportedLanguages = [_defaultLanguage];
    }

    public void Initialize(string languageOverride)
    {
        if (_isInitialized)
        {
            return;
        }

        LanguageOverride = string.IsNullOrWhiteSpace(languageOverride)
            ? null
            : new UiTestAppLanguage(languageOverride);
        StartupLanguage = LanguageOverride ?? _defaultLanguage;
        RequestedLanguage = StartupLanguage;
        _isInitialized = true;
    }

    public string GetString(string resourceKey)
    {
        return resourceKey.StartsWith("CaptureMemory_", StringComparison.Ordinal) || resourceKey.StartsWith("CaptureDetails_", StringComparison.Ordinal) ||
            resourceKey.StartsWith("CapturePane_", StringComparison.Ordinal) || resourceKey.StartsWith("CaptureNaming_", StringComparison.Ordinal)
            ? WinUIResourceLoader.GetString(ref _resources, resourceKey, resourceKey) : resourceKey;
    }

    public void OverrideLanguage(IAppLanguage? language)
    {
        LanguageOverride = language;
        RequestedLanguage = language ?? _defaultLanguage;
    }

    private sealed record UiTestAppLanguage(string Value) : IAppLanguage;
}

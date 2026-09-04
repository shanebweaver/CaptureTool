using CaptureTool.Application.Abstractions.Localization;

namespace CaptureTool.Infrastructure.Analysis.FoundryLocal.Tests;

[TestClass]
public sealed class FoundryLocalSpeechLanguagePolicyTests
{
    [TestMethod]
    [DataRow("de-DE", "de")]
    [DataRow("en-US", "en")]
    [DataRow("es-ES", "es")]
    [DataRow("fr-FR", "fr")]
    [DataRow("ru-RU", "ru")]
    [DataRow("zh-CN", "zh")]
    public void GetLanguageHint_MapsSupportedAppLanguageToWhisperHint(
        string appLanguage,
        string expectedHint)
    {
        var policy = new FoundryLocalSpeechLanguagePolicy(
            new StubLocalizationService(appLanguage));

        Assert.AreEqual(
            expectedHint,
            policy.GetLanguageHint(FoundryLocalSpeechModelConfiguration.Whisper));
    }

    [TestMethod]
    public void GetLanguageHint_UnknownAppLanguageUsesDeterministicWhisperFallback()
    {
        var policy = new FoundryLocalSpeechLanguagePolicy(
            new StubLocalizationService("ja-JP"));

        Assert.AreEqual(
            "en",
            policy.GetLanguageHint(FoundryLocalSpeechModelConfiguration.Whisper));
    }

    [TestMethod]
    public void GetLanguageHint_NemotronKeepsAutoDetectionInsideCandidatePolicy()
    {
        var policy = new FoundryLocalSpeechLanguagePolicy(
            new StubLocalizationService("fr-FR"));

        Assert.AreEqual(
            "auto",
            policy.GetLanguageHint(
                FoundryLocalSpeechModelConfiguration.NemotronMultilingual));
    }

    private sealed class StubLocalizationService(string language) : ILocalizationService
    {
        private readonly IAppLanguage _language = new StubAppLanguage(language);

        public IAppLanguage? LanguageOverride => _language;

        public IAppLanguage? RequestedLanguage => _language;

        public IAppLanguage? StartupLanguage => _language;

        public IAppLanguage? DefaultLanguage => _language;

        public IAppLanguage[] SupportedLanguages => [_language];

        public void Initialize(string languageOverride)
        {
        }

        public string GetString(string resourceKey) => resourceKey;

        public void OverrideLanguage(IAppLanguage? language)
        {
        }
    }

    private sealed record StubAppLanguage(string Value) : IAppLanguage;
}

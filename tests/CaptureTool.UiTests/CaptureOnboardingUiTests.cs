using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;
using System.Xml.Linq;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    public void CaptureOnboarding_ConsentAndCaptureCreationStayIdleAcrossRestart()
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
            "capture-explicit-actions", Guid.NewGuid().ToString("N"));
        string data = Path.Combine(isolated, "data"), temp = Path.Combine(isolated, "temp");
        Directory.CreateDirectory(data); Directory.CreateDirectory(temp);
        string fixture = Path.Combine(isolated, "capture.png"); CreateOcrFixtureImage(fixture);
        using var automation = new UIA3Automation();
        for (int launch = 0; launch < 2; launch++)
        {
            using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US", onboarding: true, detailsFixture: true, captureFixture: true);
            var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
            WaitForElement(window, automation, "ImageEdit_CommandBar", InteractionTimeout); MaximizeWindow(window);
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryConsentDialog")));
            WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
            WaitForElement(window, automation, "AppMenu_SettingsItem", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
            var consent = WaitForElement(window, automation, "CaptureMemoryConsent", InteractionTimeout);
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryScanning")));
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureNamingToggle")));
            if (launch == 0)
            {
                consent.Patterns.Toggle.Pattern.Toggle();
                var dialog = WaitForElement(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
                WaitForElementByName(dialog, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
                WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
            }
            WaitFor(() => { var saved = WaitForElement(window, automation, "CaptureMemoryConsent", InteractionTimeout); return saved.IsEnabled && saved.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On ? window : null; }, InteractionTimeout, "consent saved");
            Assert.IsFalse(WaitForElement(window, automation, "CaptureMemoryDelete", InteractionTimeout).IsEnabled);
            Assert.IsFalse(Directory.EnumerateFiles(data, "*.analysis", SearchOption.AllDirectories).Any());
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryProgress")));
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    public void CaptureOnboarding_DisabledSettingsStayQuietWithUnavailableStorage()
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
            "capture-settings-unavailable", Guid.NewGuid().ToString("N"));
        string data = Path.Combine(isolated, "data"), temp = Path.Combine(isolated, "temp");
        Directory.CreateDirectory(Path.Combine(data, "CaptureAnalysis")); Directory.CreateDirectory(temp);
        string control = Path.Combine(data, "CaptureAnalysis", "control.bin");
        File.WriteAllText(control, "unreadable test control");
        string fixture = Path.Combine(isolated, "capture.png"); CreateOcrFixtureImage(fixture);
        var strings = XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", "en-US", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(x => (string)x.Attribute("name")!, x => x.Element("value")!.Value);
        using var automation = new UIA3Automation();
        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US");
        Window window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
        Element("ImageEdit_CommandBar");
        for (int visit = 0; visit < 2; visit++)
        {
            Menu("AppMenu_SettingsItem");
            Assert.AreEqual(ToggleState.Off, Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.ToggleState.Value);
            WaitFor(() => Element("CaptureMemoryDelete").IsEnabled ? window : null, InteractionTimeout, "storage status refreshed");
            Thread.Sleep(300);
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByName(strings["CaptureMemory_Error_Storage"])),
                "Merely visiting Settings must not report dormant storage failures.");
            if (visit == 0) Menu("AppMenu_HomeItem");
        }
        Assert.AreEqual("unreadable test control", File.ReadAllText(control), "Observation must not reset protected storage.");
        Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.Toggle();
        var dialog = Element("CaptureMemoryConsentDialog");
        WaitForElementByName(dialog, automation, strings["CaptureMemory_ConsentAccept"], InteractionTimeout).AsButton().Invoke();
        WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
        WaitForElementByName(window, automation, strings["CaptureMemory_Error_Storage"], InteractionTimeout);
        Assert.AreEqual("unreadable test control", File.ReadAllText(control), "Explicit enable must preserve unreadable data too.");

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Menu(string id)
        {
            window.Focus(); WaitForElementByName(window, automation, strings["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
            Element(id).Patterns.Invoke.Pattern.Invoke();
        }
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE")]
    [DataRow("es-ES")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    [DataRow("zh-CN")]
    [TestCategory("UI")]
    public void CaptureOnboarding_ExplicitActionConsentAndIndependentOcr(string language)
    {
        RequireUiTestLanguage(language);
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "capture-onboarding", language);
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        string data = Path.Combine(isolated, "data"), temp = Path.Combine(isolated, "temp");
        Directory.CreateDirectory(data); Directory.CreateDirectory(temp);
        string fixture = Path.Combine(isolated, "capture.png"); CreateOcrFixtureImage(fixture);
        using (var stream = File.Create(Path.Combine(data, "RecentCaptures.json")))
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray(); json.WriteStartObject(); json.WriteString("FilePath", fixture);
            json.WriteNumber("CaptureFileType", 0); json.WriteNumber("Origin", 0); json.WriteString("LastActivityUtc", DateTime.UtcNow);
            json.WriteEndObject(); json.WriteEndArray();
        }
        var strings = XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", language, "Resources.resw"))
            .Root!.Elements("data").ToDictionary(x => (string)x.Attribute("name")!, x => x.Element("value")!.Value);
        using var automation = new UIA3Automation();
        Window window = null!;
        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, language, detailsFixture: true, onboarding: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar");
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryConsentDialog")), "Launch never requests AI consent.");
            foreach (var key in strings.Keys.Where(key => key.StartsWith("CaptureMemory_Error_")))
                Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByName(strings[key])), "No startup analysis error before opting in.");
            Element("ImageEdit_TextExtractionButton").Click();
            Answer("AiFeatureConsentDialog", "AiFeatureConsentDialog_AllowButton");
            Element("ImageEdit_TextExtractionOverlayMarker"); Element("ImageCanvas_QrCodeCopyButton_0");
            Element("ImageEdit_TextExtractionCopyAllButton").Patterns.Invoke.Pattern.Invoke();
            WaitFor(() => ReadDetailsClipboard()?.Contains("OCR MODE") == true ? window : null, InteractionTimeout, "standalone OCR before analysis consent");
            Screenshot("standalone-ocr");
        }
        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, language, detailsFixture: true, onboarding: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar"); Thread.Sleep(500);
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryConsentDialog")), "Dismissed welcome must stay dismissed after restart.");
            Menu("AppMenu_SettingsItem");
            Assert.AreEqual(ToggleState.On, Element("TextExtractionConsent").Patterns.Toggle.Pattern.ToggleState.Value);
            Element("TextExtractionConsent").Patterns.Toggle.Pattern.Toggle();
            WaitFor(() => Element("TextExtractionConsent").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? window : null,
                InteractionTimeout, "standalone OCR revoked from Settings");
            Element("TextExtractionConsent").Patterns.Toggle.Pattern.Toggle();
            Answer("AiFeatureConsentDialog", "AiFeatureConsentDialog_DontAllowButton");
            WaitFor(() => Element("TextExtractionConsent").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? window : null,
                InteractionTimeout, "declined standalone OCR stays off");
            Element("TextExtractionConsent").Patterns.Toggle.Pattern.Toggle();
            Answer("AiFeatureConsentDialog", "AiFeatureConsentDialog_AllowButton");
            WaitFor(() => Element("TextExtractionConsent").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On ? window : null,
                InteractionTimeout, "standalone OCR granted from Settings");
            Assert.AreEqual(ToggleState.Off, Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.ToggleState.Value);
            Assert.AreEqual(ToggleState.Off, Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.ToggleState.Value);
            WaitForElementByName(window, automation, strings["AppTheme_Dark"], InteractionTimeout).Patterns.SelectionItem.Pattern.Select();
            Menu("AppMenu_HomeItem");
            WaitFor(() => Element("Home_RecentCaptures").FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)), InteractionTimeout, "recent capture").DoubleClick();
            Element("ImageEdit_CommandBar");
            if (Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_NARROW") == "1")
            {
                window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
                window.Patterns.Transform.Pattern.Resize(680, 840);
            }
            Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
            Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
            Screenshot("text-invitation");
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryConsentDialog")), "Opening Text only reads saved results.");
            Element("CaptureAction_Text").Patterns.Invoke.Pattern.Invoke();
            Element("CaptureMemoryConsentDialog"); Screenshot("welcome-dark");
            Answer("CaptureMemoryConsentDialog", "CaptureWelcome_NotNow");
            WaitFor(() => Element("CaptureAction_Text").IsEnabled ? Element("CaptureAction_Text") : null, InteractionTimeout, "invitation command completed").Patterns.Invoke.Pattern.Invoke();
            Answer("CaptureMemoryConsentDialog", "CaptureMemory_ConsentAccept");
            var search = Element("CapturePane_Search").AsTextBox(); search.Text = "INV-2048";
            WaitFor(() => Element("CapturePane_CopyResults").IsEnabled ? window : null, InteractionTimeout, "saved analysis text");
            Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
            WaitFor(() => ReadDetailsClipboard()?.Contains("INV-2048") == true ? window : null, InteractionTimeout, "pipeline text remains independent");
            MaximizeWindow(window);
            Element("ImageEdit_TextExtractionButton").Click();
            Element("ImageEdit_TextExtractionOverlayMarker");
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("AiFeatureConsentDialog")), "Standalone consent persists independently.");
        }
        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Answer(string id, string key)
        {
            var dialog = Element(id);
            WaitForElementByName(dialog, automation, strings[key], InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, id, InteractionTimeout);
        }
        void Menu(string id)
        {
            window.Focus(); WaitForElementByName(window, automation, strings["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
            Element(id).Patterns.Invoke.Pattern.Invoke();
        }
        void Screenshot(string name)
        {
            Thread.Sleep(300); string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }
}

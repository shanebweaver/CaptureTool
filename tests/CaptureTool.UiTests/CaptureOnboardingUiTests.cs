using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;
using System.Xml.Linq;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE")]
    [DataRow("es-ES")]
    [DataRow("fr-FR")]
    [DataRow("ru-RU")]
    [DataRow("zh-CN")]
    [TestCategory("UI")]
    public void CaptureOnboarding_FirstLaunchIndependentOcrAndOnDemand(string language)
    {
        if (Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_LANGUAGE") is { Length: > 0 } requested && requested != language) Assert.Inconclusive("Targeting another language.");
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
            Element("CaptureMemoryEnableScanningDialog"); Screenshot("welcome-light");
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
            window.Patterns.Transform.Pattern.Resize(680, 840);
            Screenshot("welcome-narrow");
            Assert.IsFalse(WaitForElementByName(Element("CaptureMemoryEnableScanningDialog"), automation, strings["CaptureWelcome_NotNow"], InteractionTimeout).IsOffscreen);
            MaximizeWindow(window);
            Answer("CaptureMemoryEnableScanningDialog", "CaptureWelcome_NotNow");
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
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryEnableScanningDialog")), "Dismissed welcome must stay dismissed after restart.");
            Menu("AppMenu_SettingsItem");
            Assert.AreEqual(ToggleState.Off, Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.ToggleState.Value);
            Assert.AreEqual(ToggleState.Off, Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.ToggleState.Value);
            WaitForElementByName(window, automation, "AppTheme_Dark", InteractionTimeout).Patterns.SelectionItem.Pattern.Select();
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
            Element("CaptureMemoryEnableScanningDialog"); Screenshot("welcome-dark");
            Answer("CaptureMemoryEnableScanningDialog", "CaptureWelcome_NotNow");
            WaitFor(() => Element("CapturePane_EnableAnalysis").IsEnabled ? Element("CapturePane_EnableAnalysis") : null, InteractionTimeout, "invitation command completed").Patterns.Invoke.Pattern.Invoke();
            Answer("CaptureMemoryEnableScanningDialog", "CaptureMemory_EnableScanningAccept");
            Answer("CaptureMemoryScanExistingDialog", "CaptureMemory_ScanExistingAccept");
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

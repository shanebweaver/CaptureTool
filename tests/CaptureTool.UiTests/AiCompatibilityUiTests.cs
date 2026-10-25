using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Xml.Linq;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow("en-US", "legacy-ocr")]
    [DataRow("de-DE", "legacy-ocr")]
    [DataRow("es-ES", "legacy-ocr")]
    [DataRow("fr-FR", "legacy-ocr")]
    [DataRow("ru-RU", "legacy-ocr")]
    [DataRow("zh-CN", "legacy-ocr")]
    [DataRow("en-US", "none")]
    [TestCategory("UI")]
    public void AiCompatibility_ShowsOnlySupportedActionsAndConsentBenefits(string language, string profile)
    {
        RequireUiTestLanguage(language);
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "ai-compatibility", language, profile, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string fixture = Path.Combine(isolated, "capture.png"); CreateOcrFixtureImage(fixture);
        var resources = XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", language, "Resources.resw"))
            .Root!.Elements("data").ToDictionary(item => (string)item.Attribute("name")!, item => item.Element("value")!.Value);
        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, Path.Combine(isolated, "data"),
            Path.Combine(isolated, "temp"), language, detailsFixture: true, aiProfile: profile);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        Element("ImageEdit_CommandBar"); MaximizeWindow(window);
        Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
        if (profile == "legacy-ocr")
        {
            Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
            Element("CaptureAction_ScanText").Patterns.Invoke.Pattern.Invoke();
            var dialog = Element("CaptureMemoryConsentDialog");
            WaitForElementByName(dialog, automation, resources["CaptureWelcome_Text.Text"], InteractionTimeout);
            Assert.IsNull(dialog.FindFirstDescendant(automation.ConditionFactory.ByName(resources["CaptureWelcome_Recording.Text"])));
            Assert.IsNull(dialog.FindFirstDescendant(automation.ConditionFactory.ByName(resources["CaptureWelcome_Names.Text"])));
            Thread.Sleep(250);
            string screenshot = Path.Combine(isolated, "consent.png");
            window.CaptureToFile(screenshot); TestContext.AddResultFile(screenshot);
            WaitForElementByName(dialog, automation, resources["CaptureWelcome_NotNow"], InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
        }
        else Thread.Sleep(500); // Let the pane's initial async support queries settle.

        AssertAbsent("CaptureAction_Name");
        AssertAbsent("CapturePane_SummaryTab");
        AssertAbsent("CaptureAction_AltText");
        if (profile == "none") AssertAbsent("CapturePane_TextTab");
        WaitForElementByName(window, automation, resources["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
        Element("AppMenu_SettingsItem").Patterns.Invoke.Pattern.Invoke();
        Element("OptionalUsageDataToggleSwitch");
        if (profile == "legacy-ocr") Element("CaptureMemoryConsent");
        else
        {
            Thread.Sleep(250);
            AssertAbsent("CaptureMemoryConsent");
            AssertAbsent("CaptureMemoryDelete");
        }

        AutomationElement Element(string id)
        {
            try { return WaitForElement(window, automation, id, InteractionTimeout); }
            catch
            {
                string screenshot = Path.Combine(isolated, "failure.png");
                window.CaptureToFile(screenshot); TestContext.AddResultFile(screenshot);
                throw;
            }
        }
        void AssertAbsent(string id) => Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(id)), id);
    }
}

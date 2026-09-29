using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Text.Json;
using System.Runtime.InteropServices;
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
    public void CaptureMemory_ConsentLocalProgressAndDeleteRemainConsistentAfterNavigation(string language)
    {
        RequireUiTestLanguage(language);
        if (!ShouldRunUiTests()) Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "capture-memory", language);
        var resources = XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", language, "Resources.resw"))
            .Root!.Elements("data").ToDictionary(item => (string)item.Attribute("name")!, item => item.Element("value")!.Value);
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        string data = Path.Combine(isolated, "data");
        string temp = Path.Combine(isolated, "temp");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(temp);
        string fixture = Path.Combine(isolated, "captured.png");
        CreateOcrFixtureImage(fixture);
        using (var stream = File.Create(Path.Combine(data, "RecentCaptures.json")))
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray();
            json.WriteStartObject();
            json.WriteString("FilePath", fixture);
            json.WriteNumber("CaptureFileType", 0);
            json.WriteNumber("Origin", 0);
            json.WriteString("LastActivityUtc", DateTime.UtcNow);
            json.WriteEndObject();
            json.WriteEndArray();
        }

        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, language);
        using var automation = new UIA3Automation();
        Window window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        window.Focus();
        MaximizeWindow(window);
        WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
        OpenSettings();
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryScanning")));
        AutomationElement consent = Element("CaptureMemoryConsent");
        AutomationElement delete = Element("CaptureMemoryDelete");
        Assert.HasCount(1, window.FindAllDescendants(
            automation.ConditionFactory.ByControlType(ControlType.CheckBox)), "All local AI features share one consent checkbox.");
        Assert.AreEqual(ToggleState.Off, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.IsFalse(delete.IsEnabled);
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("TextExtractionConsent")));
        Assert.IsFalse(string.IsNullOrWhiteSpace(consent.Name), "Consent needs an accessible name.");
        Assert.AreEqual(resources["CaptureMemory_Consent.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"], consent.Name);
        Assert.AreEqual(resources["CaptureMemory_Delete.Content"], delete.Name);
        consent.Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "CaptureMemory_ConsentAccept");
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryScan")));
        WaitForElementByName(window, automation, resources["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
        WaitForElement(window, automation, "AppMenu_HomeItem", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
        var recent = Element("Home_RecentCaptures");
        WaitFor(() => recent.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)), InteractionTimeout, "recent capture").DoubleClick();
        Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
        Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
        Element("CaptureAction_ScanText").Patterns.Invoke.Pattern.Invoke();
        AutomationElement progress = WaitFor(() => window.FindFirstDescendant(
            automation.ConditionFactory.ByAutomationId("CaptureAction_ScanText_Loading")) is { IsOffscreen: false } visible ? visible : null,
            InteractionTimeout, "text action progress visible in the pane");
        Assert.IsFalse(progress.IsOffscreen, "Analysis progress should be visible beside the text action.");
        Assert.IsFalse(progress.Patterns.Invoke.IsSupported, "Progress must be passive.");
        Assert.IsFalse(progress.Patterns.Toggle.IsSupported);
        Assert.IsFalse(progress.Properties.IsKeyboardFocusable.Value, "Passive progress must not enter the tab order.");
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryProgress")),
            "Local AI actions should not show a global analysis snackbar.");
        SaveScreenshot("loading");
        WaitForElementRemoved(window, automation, "CaptureAction_ScanText_Loading", InteractionTimeout);
        OpenSettings();
        consent = Element("CaptureMemoryConsent");
        delete = Element("CaptureMemoryDelete");
        Assert.AreEqual(ToggleState.On, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        WaitFor(() => delete.IsEnabled ? delete : null, InteractionTimeout, "deletion enabled after analysis");

        consent.Patterns.Toggle.Pattern.Toggle();
        Confirm("DeleteMetadata", "CaptureMemory_Cancel");
        WaitFor(() => delete.IsEnabled ? delete : null, InteractionTimeout, "retained metadata available after declining deletion");
        delete.Patterns.Invoke.Pattern.Invoke();
        Confirm("DeleteMetadata", "CaptureMemory_DeleteMetadataAccept");
        WaitFor(() => !delete.IsEnabled && !Directory.EnumerateFiles(Path.Combine(data, "CaptureAnalysis"), "*.analysis", SearchOption.AllDirectories).Any()
            ? delete : null, InteractionTimeout, "deletion completes");
        Assert.IsTrue(File.Exists(fixture), "Deleting analysis must keep capture media.");

        // Consent was already revoked; deleting metadata does not grant it again.
        Assert.AreEqual(ToggleState.Off, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        WaitForElementByName(window, automation, resources["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
        WaitForElement(window, automation, "AppMenu_HomeItem", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
        Element("Home_RecentCaptures");
        OpenSettings();
        consent = Element("CaptureMemoryConsent");
        delete = Element("CaptureMemoryDelete");
        Assert.AreEqual(ToggleState.Off, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.AreEqual(ToggleState.Off, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.IsFalse(delete.IsEnabled);
        consent.Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "CaptureWelcome_NotNow");
        WaitFor(() => consent.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? consent : null,
            InteractionTimeout, "cancelled enable restores off state");
        consent.Focus();
        SaveScreenshot("settings");
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        window.Patterns.Transform.Pattern.Resize(900, 950);
        OpenSettings();
        SaveScreenshot("settings-narrow");
        WaitForElementByName(window, automation, resources["AppTheme_Dark"], InteractionTimeout).Patterns.SelectionItem.Pattern.Select();
        Thread.Sleep(250); // Allow the theme change to render before the visual artifact is captured.
        SaveScreenshot("settings-dark");

        void OpenSettings()
        {
            window.Focus();
            WaitForElementByName(window, automation, resources["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
            WaitForElement(window, automation, "AppMenu_SettingsItem", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
            AutomationElement target = Element("CaptureMemoryConsent");
            var scroll = window.FindAllDescendants().First(element => element.Patterns.Scroll.IsSupported &&
                element.Patterns.Scroll.Pattern.VerticallyScrollable.Value).Patterns.Scroll.Pattern;
            for (int i = 0; i < 15 && target.IsOffscreen; i++)
            {
                scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                Thread.Sleep(100);
            }
            Assert.IsFalse(target.IsOffscreen, "Capture Memory settings should be visible.");
            target.Focus();
        }
        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Confirm(string prompt, string answer)
        {
            AutomationElement dialog = Element("CaptureMemory" + prompt + "Dialog");
            WaitForElementByName(dialog, automation, resources[answer], InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemory" + prompt + "Dialog", InteractionTimeout);
        }
        void SaveScreenshot(string name)
        {
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path);
            TestContext.AddResultFile(path);
        }
    }

    private sealed class DesktopDpiScope : IDisposable
    {
        private readonly nint _previous = SetThreadDpiAwarenessContext(new nint(-4));
        public void Dispose() => SetThreadDpiAwarenessContext(_previous);
        [DllImport("user32.dll")]
        private static extern nint SetThreadDpiAwarenessContext(nint context);
    }
}

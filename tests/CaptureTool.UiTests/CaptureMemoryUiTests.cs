using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    public void CaptureMemory_ConsentScanProgressAndDeleteRemainConsistentAfterNavigation()
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "capture-memory");
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

        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp);
        using var automation = new UIA3Automation();
        Window window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        window.Focus();
        MaximizeWindow(window);
        WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
        OpenSettings();
        AutomationElement toggle = Element("CaptureMemoryScanning");
        AutomationElement consent = Element("CaptureMemoryConsent");
        AutomationElement scan = Element("CaptureMemoryScan");
        AutomationElement delete = Element("CaptureMemoryDelete");
        Assert.HasCount(1, window.FindAllDescendants(
            automation.ConditionFactory.ByControlType(ControlType.CheckBox)), "AI settings must expose a single consent checkbox.");
        Assert.AreEqual(ToggleState.Off, toggle.Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.IsFalse(scan.IsEnabled);
        Assert.IsFalse(delete.IsEnabled);

        toggle.Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "Allow");
        Confirm("ScanExisting", "Analyze captures");
        AutomationElement progress = Element("CaptureMemoryProgress");
        Assert.IsFalse(progress.IsOffscreen, "Analysis progress should be visible in the shell.");
        Assert.IsFalse(progress.Patterns.Invoke.IsSupported, "Progress must be passive.");
        Assert.IsFalse(progress.Patterns.Toggle.IsSupported);
        SaveScreenshot("loading");
        WaitForElementRemoved(window, automation, "CaptureMemoryProgress", InteractionTimeout);
        Assert.AreEqual(ToggleState.On, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        WaitFor(() => delete.IsEnabled ? delete : null, InteractionTimeout, "deletion enabled after analysis");
        Assert.IsTrue(scan.IsEnabled);

        toggle.Patterns.Toggle.Pattern.Toggle();
        Confirm("DeleteMetadata", "Cancel");
        WaitFor(() => !scan.IsEnabled ? scan : null, InteractionTimeout, "scan disabled with scanning off");
        WaitFor(() => delete.IsEnabled ? delete : null, InteractionTimeout, "retained metadata available after declining deletion");
        delete.Patterns.Invoke.Pattern.Invoke();
        Confirm("DeleteMetadata", "Delete information");
        WaitFor(() => !delete.IsEnabled && !Directory.EnumerateFiles(Path.Combine(data, "CaptureAnalysis"), "*.analysis", SearchOption.AllDirectories).Any()
            ? delete : null, InteractionTimeout, "deletion completes");
        Assert.IsTrue(File.Exists(fixture), "Deleting analysis must keep capture media.");

        // Revoking consent with no metadata requires no further deletion prompt.
        consent.Focus();
        consent.Click();
        WaitFor(() => consent.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? consent : null,
            InteractionTimeout, "consent revoked");
        WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
        WaitForElement(window, automation, "AppMenu_HomeItem", InteractionTimeout).Click();
        OpenSettings();
        toggle = Element("CaptureMemoryScanning");
        consent = Element("CaptureMemoryConsent");
        scan = Element("CaptureMemoryScan");
        delete = Element("CaptureMemoryDelete");
        Assert.AreEqual(ToggleState.Off, toggle.Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.AreEqual(ToggleState.Off, consent.Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.IsFalse(scan.IsEnabled);
        Assert.IsFalse(delete.IsEnabled);
        toggle.Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "Cancel");
        WaitFor(() => toggle.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? toggle : null,
            InteractionTimeout, "cancelled enable restores off state");
        consent.Focus();
        SaveScreenshot("settings");
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        window.Patterns.Transform.Pattern.Resize(900, 950);
        OpenSettings();
        SaveScreenshot("settings-narrow");
        WaitForElementByName(window, automation, "AppTheme_Dark", InteractionTimeout).Patterns.SelectionItem.Pattern.Select();
        Thread.Sleep(250); // Allow the theme change to render before the visual artifact is captured.
        SaveScreenshot("settings-dark");

        void OpenSettings()
        {
            window.Focus();
            WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
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
            WaitForElementByName(dialog, automation, answer, InteractionTimeout).AsButton().Invoke();
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

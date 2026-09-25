using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Runtime.InteropServices;
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
    public void CapturePane_LocalDetailsAndAnalysisLifecycle(string language)
    {
        if (Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_LANGUAGE") is { Length: > 0 } requested && requested != language)
            Assert.Inconclusive("A different UI language was requested for this targeted run.");
        if (!ShouldRunUiTests()) Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "capture-details", language);
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
            json.WriteStartArray(); json.WriteStartObject();
            json.WriteString("FilePath", fixture); json.WriteNumber("CaptureFileType", 0); json.WriteNumber("Origin", 0);
            json.WriteString("LastActivityUtc", DateTime.UtcNow); json.WriteEndObject(); json.WriteEndArray();
        }
        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, language, detailsFixture: true);
        using var automation = new UIA3Automation();
        Window window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        window.Focus();
        MaximizeWindow(window);
        WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
        AutomationElement dialog = OpenDetails();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Unknown"], InteractionTimeout);
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Media_Image"], InteractionTimeout);
        Screenshot("local-details");
        WaitForElementByName(dialog, automation, resources["CapturePane_CopyPath.Content"], InteractionTimeout).AsButton().Invoke();
        WaitFor(() => ReadDetailsClipboard() == fixture ? dialog : null, InteractionTimeout, "source path copied without AI consent");
        CloseDetails();

        OpenSettings();
        Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "CaptureMemory_ConsentAccept");
        Confirm("ScanExisting", "CaptureMemory_ScanExistingAccept");
        GoHome();
        OpenCapture();
        dialog = OpenDetails();
        WaitForElementByName(dialog, automation, "Invoice INV-2048 totals USD 125.00 and is due on 2026-10-15.", InteractionTimeout);
        Screenshot("summary");
        Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
        var search = Element("CapturePane_Search").AsTextBox();
        search.Text = "INV-2048";
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard() == "Contoso invoice. Reference: INV-2048. Total USD 125.00. Due 2026-10-15." ? dialog : null,
            InteractionTimeout, "all matching text copied");
        var location = WaitFor(() => dialog.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button).And(automation.ConditionFactory.ByName(resources["CaptureDetails_RecognizedText"])))
            .FirstOrDefault(item => item.IsEnabled), InteractionTimeout, "source location");
        location.Patterns.Invoke.Pattern.Invoke();
        Screenshot("text-location");
        search.Text = "absent phrase";
        WaitFor(() => !Element("CapturePane_CopyResults").IsEnabled ? dialog : null, InteractionTimeout, "empty search");
        search.Text = string.Empty;
        WaitFor(() => dialog.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button).And(automation.ConditionFactory.ByName(resources["CaptureDetails_QrCode"]))).FirstOrDefault(item => item.IsEnabled), InteractionTimeout, "QR location after filtering");
        Screenshot("source-text");
        Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.Select();
        CloseDetails();
        OpenSettings();
        WaitForElementByName(window, automation, "AppTheme_Dark", InteractionTimeout).Patterns.SelectionItem.Pattern.Select();
        GoHome(); OpenCapture();
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        window.Patterns.Transform.Pattern.Resize(720, 760);
        dialog = OpenDetails();
        Screenshot("compact-dark");
        CloseDetails();
        MaximizeWindow(window);
        OpenSettings();
        Element("CaptureMemoryDelete").Patterns.Invoke.Pattern.Invoke();
        Confirm("DeleteMetadata", "CaptureMemory_DeleteMetadataAccept");
        WaitFor(() => !Element("CaptureMemoryDelete").IsEnabled ? window : null, InteractionTimeout, "metadata deletion");
        GoHome(); OpenCapture();
        dialog = OpenDetails();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Media_Image"], InteractionTimeout);
        Assert.IsNull(dialog.FindFirstDescendant(automation.ConditionFactory.ByName("Invoice INV-2048 totals USD 125.00 and is due on 2026-10-15.")));
        Screenshot("after-delete");
        CloseDetails();

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Menu(string id)
        {
            window.Focus();
            WaitForElementByName(window, automation, resources["AppMenu_FileMenuItem.Title"], InteractionTimeout).Click();
            Element(id).Patterns.Invoke.Pattern.Invoke();
        }
        void GoHome() { Menu("AppMenu_HomeItem"); Element("Home_RecentCaptures"); }
        void OpenSettings()
        {
            Menu("AppMenu_SettingsItem");
            var target = Element("CaptureMemoryConsent");
            ScrollTo(window, target);
        }
        void OpenCapture()
        {
            var grid = Element("Home_RecentCaptures");
            var item = WaitFor(() => grid.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)),
                InteractionTimeout, "recent capture");
            item.DoubleClick();
            Element("Editor_DetailsToggle");
        }
        AutomationElement OpenDetails()
        {
            var toggle = Element("Editor_DetailsToggle").Patterns.Toggle.Pattern;
            if (toggle.ToggleState.Value != ToggleState.On) toggle.Toggle();
            Thread.Sleep(300);
            window.CaptureToFile(Path.Combine(artifacts, "opened-pane.png"));
            return Element("CaptureDetailsPane");
        }
        void CloseDetails()
        {
            WaitForElementByName(dialog, automation, resources["CapturePane_Close.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"], InteractionTimeout).AsButton().Invoke();
            WaitFor(() => Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? window : null,
                InteractionTimeout, "closed details pane");
        }
        void Confirm(string prompt, string answer)
        {
            var promptDialog = Element("CaptureMemory" + prompt + "Dialog");
            WaitForElementByName(promptDialog, automation, resources[answer], InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemory" + prompt + "Dialog", InteractionTimeout);
        }
        void ScrollTo(AutomationElement root, AutomationElement target)
        {
            var scroll = root.FindAllDescendants().First(element => element.Patterns.Scroll.IsSupported &&
                element.Patterns.Scroll.Pattern.VerticallyScrollable.Value).Patterns.Scroll.Pattern;
            for (int i = 0; i < 30 && target.IsOffscreen; i++)
            {
                scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                Thread.Sleep(60);
            }
            Assert.IsFalse(target.IsOffscreen);
        }
        void Screenshot(string name)
        {
            Thread.Sleep(200);
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path);
            TestContext.AddResultFile(path);
        }
    }

    private static string? ReadDetailsClipboard()
    {
        if (!DetailsClipboard.OpenClipboard(0)) return null;
        try
        {
            nint handle = DetailsClipboard.GetClipboardData(13);
            if (handle == 0) return null;
            nint pointer = DetailsClipboard.GlobalLock(handle);
            try { return Marshal.PtrToStringUni(pointer); }
            finally { DetailsClipboard.GlobalUnlock(handle); }
        }
        finally { DetailsClipboard.CloseClipboard(); }
    }
    private static class DetailsClipboard
    {
        [DllImport("user32.dll")] public static extern bool OpenClipboard(nint window);
        [DllImport("user32.dll")] public static extern bool CloseClipboard();
        [DllImport("user32.dll")] public static extern nint GetClipboardData(uint format);
        [DllImport("kernel32.dll")] public static extern nint GlobalLock(nint handle);
        [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(nint handle);
    }
}

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
    public void CaptureDetails_ReadCopyEvidenceAndChangedSource(string language)
    {
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
        GoHome();
        AutomationElement dialog = OpenDetails();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Empty"], InteractionTimeout);
        Screenshot("empty");
        CloseDetails();

        OpenSettings();
        Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "CaptureMemory_ConsentAccept");
        Confirm("ScanExisting", "CaptureMemory_ScanExistingAccept");
        GoHome();
        dialog = OpenDetails();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Analyzing"], InteractionTimeout);
        WaitForElementByName(dialog, automation, "Contoso invoice", TimeSpan.FromSeconds(30));
        WaitForElementRemoved(window, automation, "CaptureMemoryProgress", TimeSpan.FromSeconds(30));
        var copy = dialog.FindAllDescendants(automation.ConditionFactory.ByName(
            resources["CaptureDetails_Copy.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"]))
            .First(element => element.Patterns.Invoke.IsSupported);
        copy.Patterns.Invoke.Pattern.Invoke();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Copied"], InteractionTimeout);
        Assert.AreEqual("Contoso invoice", ReadDetailsClipboard());
        var source = dialog.FindAllDescendants(automation.ConditionFactory.ByName(resources["CaptureDetails_Evidence.Header"]))
            .First(element => element.Patterns.ExpandCollapse.IsSupported);
        source.Patterns.ExpandCollapse.Pattern.Expand();
        WaitForElementByName(dialog, automation, "Contoso invoice. Reference: INV-2048. Total USD 125.00. Due 2026-10-15.", InteractionTimeout);
        Screenshot("overview");
        source.Patterns.ExpandCollapse.Pattern.Collapse();
        var facts = WaitForElementByName(dialog, automation, resources["CaptureDetails_FactsHeading.Text"], InteractionTimeout);
        ScrollTo(dialog, facts);
        Screenshot("facts");
        var recognized = dialog.FindAllDescendants(automation.ConditionFactory.ByName(resources["CaptureDetails_RecognizedText"]))
            .First(element => element.Patterns.ExpandCollapse.IsSupported);
        ScrollTo(dialog, recognized);
        recognized.Patterns.ExpandCollapse.Pattern.Expand();
        var copyAll = WaitForElementByName(recognized, automation, resources["CaptureDetails_CopyAll.Content"], InteractionTimeout);
        ScrollTo(dialog, copyAll);
        copyAll.Patterns.Invoke.Pattern.Invoke();
        string expectedText = "Contoso invoice. Reference: INV-2048. Total USD 125.00. Due 2026-10-15." + Environment.NewLine +
            "Contact billing@example.com or visit https://example.com/invoice.";
        WaitFor(() => ReadDetailsClipboard() == expectedText ? dialog : null, InteractionTimeout, "complete source text on the clipboard");
        Screenshot("source-content");
        recognized.Patterns.ExpandCollapse.Pattern.Collapse();
        var properties = dialog.FindAllDescendants(automation.ConditionFactory.ByName(resources["CaptureDetails_PropertiesHeading.Header"]))
            .First(element => element.Patterns.ExpandCollapse.IsSupported);
        ScrollTo(dialog, properties);
        properties.Patterns.ExpandCollapse.Pattern.Expand();
        WaitForElementByName(dialog, automation, "3:2", InteractionTimeout);
        Screenshot("properties");
        CloseDetails();

        OpenSettings();
        WaitForElementByName(window, automation, "AppTheme_Dark", InteractionTimeout).Patterns.SelectionItem.Pattern.Select();
        GoHome();
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        window.Patterns.Transform.Pattern.Resize(720, 760);
        dialog = OpenDetails();
        WaitForElementByName(dialog, automation, "Contoso invoice", InteractionTimeout);
        Screenshot("compact-dark");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        WaitForElementRemoved(window, automation, "CaptureDetailsDialog", InteractionTimeout);
        MaximizeWindow(window);

        // Same source path, different bytes: old insights must disappear on the next verified read.
        File.AppendAllText(fixture, "changed source bytes");
        dialog = OpenDetails();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_SourceChanged"], InteractionTimeout);
        Assert.IsNull(dialog.FindFirstDescendant(automation.ConditionFactory.ByName("Contoso invoice")));
        Screenshot("changed");
        CloseDetails();
        OpenSettings();
        Element("CaptureMemoryDelete").Patterns.Invoke.Pattern.Invoke();
        Confirm("DeleteMetadata", "CaptureMemory_DeleteMetadataAccept");
        WaitFor(() => !Element("CaptureMemoryDelete").IsEnabled ? window : null, InteractionTimeout, "metadata deletion");
        GoHome();
        dialog = OpenDetails();
        WaitForElementByName(dialog, automation, resources["CaptureDetails_Empty"], InteractionTimeout);
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
        AutomationElement OpenDetails()
        {
            var grid = Element("Home_RecentCaptures");
            var item = WaitFor(() => grid.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)),
                InteractionTimeout, "recent capture");
            item.Focus();
            Keyboard.Press(VirtualKeyShort.SHIFT);
            Keyboard.Type(VirtualKeyShort.F10);
            Keyboard.Release(VirtualKeyShort.SHIFT);
            Element("Home_CaptureDetails").Patterns.Invoke.Pattern.Invoke();
            return Element("CaptureDetailsDialog");
        }
        void CloseDetails()
        {
            WaitForElementByName(dialog, automation, resources["CaptureDetails_Dialog.CloseButtonText"], InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureDetailsDialog", InteractionTimeout);
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

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Xml.Linq;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    public void CaptureNaming_SettingAutomaticTitleUserOverrideAndDeletion()
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests to run this check.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "capture-naming");
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        string data = Path.Combine(isolated, "data"), temp = Path.Combine(isolated, "temp");
        Directory.CreateDirectory(data); Directory.CreateDirectory(temp);
        string fixture = Path.Combine(isolated, "capture.png");
        CreateOcrFixtureImage(fixture);
        var resources = XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", "en-US", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(item => (string)item.Attribute("name")!, item => item.Element("value")!.Value);
        using var automation = new UIA3Automation();
        Window window = null!;
        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US", detailsFixture: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar");
            Settings();
            Assert.IsFalse(Element("CaptureNamingToggle").IsEnabled);
            Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.Toggle();
            Confirm("Consent", "CaptureMemory_ConsentAccept");
            Confirm("ScanExisting", "CaptureMemory_ScanExistingAccept");
            WaitFor(() => Element("CaptureNamingToggle").IsEnabled ? window : null, InteractionTimeout, "naming enabled by scanning consent");
            Element("CaptureNamingToggle").Patterns.Toggle.Pattern.Toggle();
            WaitFor(() => Element("CaptureNamingToggle").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On ? window : null,
                InteractionTimeout, "automatic naming enabled");
            Thread.Sleep(300); Screenshot("settings");
        }

        // This opt-in harness enrolls the fixture through the production capture-memory intake.
        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US", detailsFixture: true, captureFixture: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar"); OpenPane();
            WaitFor(() => Element("CaptureDetailsFileName").Name == "Contoso invoice" ? window : null,
                TimeSpan.FromSeconds(45), "automatic capture title");
            Screenshot("automatic-name");
            EditName("Partner demo notes");
            Screenshot("user-name");
            WaitForElementByName(window, automation, resources["CapturePane_CopyPath.Content"], InteractionTimeout).AsButton().Invoke();
            WaitFor(() => ReadDetailsClipboard() == fixture ? window : null, InteractionTimeout, "unchanged physical path");
            Menu("AppMenu_HomeItem");
            WaitForElementByName(Element("Home_RecentCaptures"), automation, "Partner demo notes", InteractionTimeout);
            Screenshot("recent-name");
            Settings();
            Element("CaptureMemoryDelete").Patterns.Invoke.Pattern.Invoke();
            Confirm("DeleteMetadata", "CaptureMemory_DeleteMetadataAccept");
            WaitFor(() => !Element("CaptureMemoryDelete").IsEnabled && Element("CaptureMemoryScan").IsEnabled &&
                !Directory.EnumerateFiles(Path.Combine(data, "CaptureAnalysis"), "*.analysis", SearchOption.AllDirectories).Any()
                ? window : null, InteractionTimeout, "metadata deletion completed before restart");
        }

        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US", detailsFixture: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar"); OpenPane();
            WaitFor(() => Element("CaptureDetailsFileName").Name == "Partner demo notes" ? window : null,
                InteractionTimeout, "chosen name survives deletion and restart");
            Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
            WaitForElementByName(Element("CaptureDetailsPane"), automation, resources["CaptureDetails_Empty"], InteractionTimeout);
            Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.Select();
            Assert.IsNull(Element("CaptureDetailsPane").FindFirstDescendant(automation.ConditionFactory.ByName(
                "Invoice INV-2048 totals USD 125.00 and is due on 2026-10-15.")), "Deleted analysis must not return after restart.");
            Screenshot("after-delete-restart");
            Settings();
            Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.Toggle();
            WaitFor(() => Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.Off ? window : null,
                InteractionTimeout, "scanning disabled");
            // A retained preference can still be switched off while scanning is off.
            Element("CaptureNamingToggle").Patterns.Toggle.Pattern.Toggle();
            WaitFor(() => !Element("CaptureNamingToggle").IsEnabled ? window : null, InteractionTimeout, "naming disabled without scanning");
        }
        Assert.IsTrue(File.Exists(fixture));

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Menu(string id)
        {
            window.Focus(); WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
            Element(id).Patterns.Invoke.Pattern.Invoke();
        }
        void Settings()
        {
            Menu("AppMenu_SettingsItem");
            var target = Element("CaptureNamingToggle");
            var scroll = window.FindAllDescendants().First(item => item.Patterns.Scroll.IsSupported && item.Patterns.Scroll.Pattern.VerticallyScrollable.Value).Patterns.Scroll.Pattern;
            for (int i = 0; i < 30 && target.IsOffscreen; i++) { scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement); Thread.Sleep(60); }
        }
        void Confirm(string prompt, string answer)
        {
            var dialog = Element("CaptureMemory" + prompt + "Dialog");
            WaitForElementByName(dialog, automation, resources[answer], InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemory" + prompt + "Dialog", InteractionTimeout);
        }
        void OpenPane()
        {
            var toggle = Element("Editor_DetailsToggle").Patterns.Toggle.Pattern;
            if (toggle.ToggleState.Value != ToggleState.On) toggle.Toggle();
            Thread.Sleep(350);
        }
        void EditName(string name)
        {
            Element("CaptureName_Edit").Patterns.Invoke.Pattern.Invoke();
            Element("CaptureName_Input").AsTextBox().Text = name;
            Element("CaptureName_Save").Patterns.Invoke.Pattern.Invoke();
            WaitFor(() => Element("CaptureDetailsFileName").Name == name ? window : null, InteractionTimeout, "user capture name");
        }
        void Screenshot(string name)
        {
            Thread.Sleep(250);
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }
}

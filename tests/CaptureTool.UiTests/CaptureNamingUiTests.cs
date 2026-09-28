using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Xml.Linq;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    public void CaptureNaming_SuggestionLoadingAcceptanceRenamesFileAndSurvivesRestart()
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
            Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.Toggle();
            Confirm("Consent", "CaptureMemory_ConsentAccept");
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureNamingToggle")));
            Assert.IsFalse(Element("CaptureMemoryDelete").IsEnabled);
            Thread.Sleep(300); Screenshot("settings");
        }

        // Production capture intake registers identity; only these button clicks request analysis.
        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US", detailsFixture: true, captureFixture: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar"); OpenPane();
            Assert.IsTrue(Element("CaptureAction_Name").IsEnabled, "The explicit name action can obtain missing image inputs.");
            double tabsTop = Element("CapturePane_DetailsTab").BoundingRectangle.Top;
            var nameBounds = Element("CaptureDetailsFileName").BoundingRectangle;
            var actionBounds = Element("CaptureAction_Name").BoundingRectangle;
            Element("CaptureName_Edit").Patterns.Invoke.Pattern.Invoke();
            Element("CaptureName_Input").AsTextBox().Text = string.Empty;
            Element("CaptureName_Save").Patterns.Invoke.Pattern.Invoke();
            WaitForElementByName(window, automation, resources["CaptureNaming_Invalid"], InteractionTimeout);
            Assert.IsNull(Element("CaptureDetailsPane").FindFirstDescendant(automation.ConditionFactory.ByName(resources["CaptureNaming_Invalid"])));
            Assert.AreEqual(tabsTop, Element("CapturePane_DetailsTab").BoundingRectangle.Top, 1, "Rename feedback must not shift the pane.");
            Screenshot("rename-validation");
            Element("CaptureName_Cancel").Patterns.Invoke.Pattern.Invoke();
            Screenshot("filename-idle");
            Element("CaptureAction_Name").Patterns.Invoke.Pattern.Invoke();
            var loading = Element("CaptureAction_Name_Loading");
            Assert.IsTrue(actionBounds.Contains(loading.BoundingRectangle), "Loading stays inside the AI button.");
            Assert.AreEqual(tabsTop, Element("CapturePane_DetailsTab").BoundingRectangle.Top, 1);
            Screenshot("filename-loading");
            try
            {
                WaitFor(() => window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureName_Input"))?.AsTextBox().Text == "Contoso invoice" ? window : null,
                    TimeSpan.FromSeconds(45), "suggested capture name");
            }
            catch { Screenshot("suggestion-timeout"); throw; }
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureDetailsFileName")));
            Assert.AreEqual(tabsTop, Element("CapturePane_DetailsTab").BoundingRectangle.Top, 1);
            Assert.AreEqual(nameBounds.Right, Element("CaptureName_Input").BoundingRectangle.Right, 1);
            Assert.IsTrue(File.Exists(fixture), "Suggestions must not rename a file before acceptance.");
            Element("CapturePane_SummaryTab").Patterns.SelectionItem.Pattern.Select();
            Assert.IsTrue(Element("CaptureAction_Summary").IsEnabled, "Name generation must leave summary generation available.");
            Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.Select();
            Screenshot("suggested-name");
            Element("CaptureName_Cancel").Patterns.Invoke.Pattern.Invoke();
            Assert.AreEqual("capture.png", Element("CaptureDetailsFileName").Name);
            Assert.AreEqual(tabsTop, Element("CapturePane_DetailsTab").BoundingRectangle.Top, 1);
            Element("CaptureAction_Name").Patterns.Invoke.Pattern.Invoke();
            Assert.AreEqual("Contoso invoice", Element("CaptureName_Input").AsTextBox().Text);
            Element("CaptureName_Save").Patterns.Invoke.Pattern.Invoke();
            string suggestedPath = Path.Combine(isolated, "Contoso invoice.png");
            WaitFor(() => File.Exists(suggestedPath) && !File.Exists(fixture) ? window : null,
                InteractionTimeout, "acceptance renames the actual file");
            fixture = suggestedPath;
            WaitFor(() => Element("CaptureDetailsFileName").Name == "Contoso invoice.png" ? window : null,
                InteractionTimeout, "editor tracks accepted filename");
            Screenshot("accepted-name");
            EditName("Partner demo notes");
            string editedPath = Path.Combine(isolated, "Partner demo notes.png");
            Assert.IsFalse(File.Exists(fixture));
            Assert.IsTrue(File.Exists(editedPath));
            fixture = editedPath;
            Screenshot("user-name");
            WaitForElementByName(window, automation, resources["CapturePane_CopyPath.Content"], InteractionTimeout).AsButton().Invoke();
            WaitFor(() => ReadDetailsClipboard() == fixture ? window : null, InteractionTimeout, "renamed physical path");
            WaitForElementByName(window, automation, resources["CaptureDetails_Copied"], InteractionTimeout);
            Screenshot("copy-notification");
            Menu("AppMenu_HomeItem");
            WaitForElementByName(Element("Home_RecentCaptures"), automation, "Partner demo notes", InteractionTimeout);
            Screenshot("recent-name");
            Settings();
            Element("CaptureMemoryDelete").Patterns.Invoke.Pattern.Invoke();
            Confirm("DeleteMetadata", "CaptureMemory_DeleteMetadataAccept");
            WaitFor(() => !Element("CaptureMemoryDelete").IsEnabled &&
                !Directory.EnumerateFiles(Path.Combine(data, "CaptureAnalysis"), "*.analysis", SearchOption.AllDirectories).Any()
                ? window : null, InteractionTimeout, "metadata deletion completed before restart");
        }

        using (var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data, temp, "en-US", detailsFixture: true))
        {
            window = WaitForMainWindow(app, automation, AppLaunchTimeout); MaximizeWindow(window);
            Element("ImageEdit_CommandBar"); OpenPane();
            WaitFor(() => Element("CaptureDetailsFileName").Name == "Partner demo notes.png" ? window : null,
                InteractionTimeout, "chosen name survives deletion and restart");
            Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
            Assert.IsTrue(Element("CaptureAction_Text").IsEnabled);
            Assert.IsFalse(Element("CapturePane_CopyResults").IsEnabled);
            Assert.IsEmpty(Element("CapturePane_Passages").FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.ListItem)));
            Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.Select();
            Assert.IsNull(Element("CaptureDetailsPane").FindFirstDescendant(automation.ConditionFactory.ByName(
                "Invoice INV-2048 totals USD 125.00 and is due on 2026-10-15.")), "Deleted analysis must not return after restart.");
            Screenshot("after-delete-restart");
            Settings();
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureMemoryScanning")));
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureNamingToggle")));
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
            var target = Element("CaptureMemoryConsent");
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
            double tabsTop = Element("CapturePane_DetailsTab").BoundingRectangle.Top;
            var nameBounds = Element("CaptureDetailsFileName").BoundingRectangle;
            Element("CaptureName_Edit").Patterns.Invoke.Pattern.Invoke();
            Element("CaptureName_Input").AsTextBox().Text = name;
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureDetailsFileName")));
            Assert.AreEqual(tabsTop, Element("CapturePane_DetailsTab").BoundingRectangle.Top, 1);
            Assert.AreEqual(nameBounds.Right, Element("CaptureName_Input").BoundingRectangle.Right, 1);
            Screenshot("rename-input");
            Element("CaptureName_Save").Patterns.Invoke.Pattern.Invoke();
            WaitFor(() => Element("CaptureDetailsFileName").Name == name + ".png" ? window : null, InteractionTimeout, "user capture name");
            Assert.AreEqual(tabsTop, Element("CapturePane_DetailsTab").BoundingRectangle.Top, 1);
        }
        void Screenshot(string name)
        {
            Thread.Sleep(250);
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }
}

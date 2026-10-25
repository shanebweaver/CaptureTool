using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Text.Json;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    public void CapturePane_TogglesFromCommandBarAndPreservesLayout()
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "details-layout");
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string fixture = Path.Combine(isolated, "capture.png");
        CreateOcrFixtureImage(fixture);
        string nextImage = Path.Combine(isolated, "next.png");
        File.Copy(fixture, nextImage);
        string data = Path.Combine(isolated, "data");
        Directory.CreateDirectory(data);
        using (var stream = File.Create(Path.Combine(data, "RecentCaptures.json")))
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray(); json.WriteStartObject(); json.WriteString("FilePath", nextImage);
            json.WriteNumber("CaptureFileType", 0); json.WriteNumber("Origin", 0);
            json.WriteString("LastActivityUtc", DateTime.UtcNow); json.WriteEndObject(); json.WriteEndArray();
        }
        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, data,
            Path.Combine(isolated, "temp"), "en-US", detailsFixture: true);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        Element("ImageEdit_CommandBar"); MaximizeWindow(window);
        Element("Editor_DetailsToggle").Click();
        Layout();
        Element("Editor_DetailsToggle").Click();
        AssertClosed();
        Element("Editor_DetailsToggle").Focus();
        Keyboard.Type(VirtualKeyShort.SPACE);
        Layout();
        Element("PART_ScrollPresenter").Click();
        Layout();
        Element("CaptureName_Edit").Patterns.Invoke.Pattern.Invoke();
        Element("CaptureName_Input").Focus();
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        WaitForElementRemoved(window, automation, "CaptureName_Input", InteractionTimeout);
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Layout();
        Screenshot("wide-image");

        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        window.Patterns.Transform.Pattern.Resize(720, 760);
        Thread.Sleep(350);
        Layout();
        Element("Editor_DetailsToggle").Click();
        AssertClosed();
        Element("Editor_DetailsToggle").Click();
        Layout();
        Element("PART_ScrollPresenter").Click();
        Layout();
        Screenshot("compact-image");
        MaximizeWindow(window);
        Layout();

        Element("ImageEdit_CropButton").Click();
        WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
        Assert.AreEqual(ToggleState.On, Element("ImageEdit_CropButton").Patterns.Toggle.Pattern.ToggleState.Value);
        Element("Editor_DetailsToggle").Click();
        Layout();
        Assert.AreEqual(ToggleState.Off, Element("ImageEdit_CropButton").Patterns.Toggle.Pattern.ToggleState.Value);
        Element("ImageEdit_TextExtractionButton").Click();
        var consent = Element("CaptureMemoryConsentDialog");
        WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
        WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
        Layout();
        Assert.IsTrue(Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.IsSelected.Value);
        Assert.IsFalse(Element("ImageEdit_TextExtractionButton").Patterns.Toggle.IsSupported);
        Element("CapturePane_Close").Patterns.Invoke.Pattern.Invoke();
        AssertClosed();

        // A different image and reopening the same image both start a fresh pane lifetime.
        for (int load = 0; load < 2; load++)
        {
            Element("Editor_DetailsToggle").Click();
            Layout();
            window.Focus();
            WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
            Element("AppMenu_HomeItem").Patterns.Invoke.Pattern.Invoke();
            var captures = Element("Home_RecentCaptures");
            WaitFor(() => captures.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)),
                InteractionTimeout, "next image").DoubleClick();
            Element("ImageEdit_CommandBar");
            AssertClosed();
        }

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Layout() => AssertDetailsLayout(window, automation, "ImageEdit_CommandBar", "ZoomSlider");
        void AssertClosed()
        {
            WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
            Assert.AreEqual(ToggleState.Off, Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.ToggleState.Value);
        }
        void Screenshot(string name)
        {
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }

    private static void AssertDetailsLayout(Window window, UIA3Automation automation, string commandBarId, string? footerId)
    {
        var pane = WaitForElement(window, automation, "CaptureDetailsPane", InteractionTimeout);
        var toolbar = WaitForElement(window, automation, commandBarId, InteractionTimeout);
        Assert.AreEqual(ToggleState.On, WaitForElement(window, automation, "Editor_DetailsToggle", InteractionTimeout).Patterns.Toggle.Pattern.ToggleState.Value);
        Assert.IsFalse(pane.IsOffscreen);
        Assert.IsGreaterThanOrEqualTo(toolbar.BoundingRectangle.Bottom - 1, pane.BoundingRectangle.Top, "Details must stay below the command bar.");
        if (footerId != null)
        {
            var footer = WaitForElement(window, automation, footerId, InteractionTimeout);
            Assert.IsFalse(footer.IsOffscreen);
            Assert.IsLessThanOrEqualTo(footer.BoundingRectangle.Top + 1, pane.BoundingRectangle.Bottom, "Details must stay above the footer.");
        }
    }
}

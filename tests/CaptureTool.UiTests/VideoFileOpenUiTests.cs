using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow(false, "default", false, "CapturePane_TextTab")]
    [DataRow(true, "default", false, "CapturePane_TextTab")]
    [DataRow(false, "compact", false, "CapturePane_TextTab")]
    [DataRow(true, "compact", false, "CapturePane_TextTab")]
    [DataRow(false, "maximized", false, "CapturePane_TextTab")]
    [DataRow(true, "maximized", false, "CapturePane_TextTab")]
    [DataRow(true, "default", true, "CapturePane_TextTab")]
    [DataRow(true, "compact", true, "CapturePane_TextTab")]
    [DataRow(true, "maximized", true, "CapturePane_TextTab")]
    [DataRow(true, "default", false, "CapturePane_SummaryTab")]
    [TestCategory("UI")]
    public void FileOpen_VideoFromImageEditor(bool detailsOpen, string windowSize, bool populateText, string tabId)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests to run this check.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
            "video-file-open", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string image = Path.Combine(isolated, "startup.png");
        CreateOcrFixtureImage(image);
        string video = Path.Combine(isolated, "recording.mp4");
        string? suppliedVideo = Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_VIDEO");
        if (!string.IsNullOrWhiteSpace(suppliedVideo))
        {
            video = Path.Combine(isolated, Path.GetFileName(suppliedVideo));
            File.Copy(suppliedVideo, video);
        }
        else
        {
            File.WriteAllBytes(video, []);
            var composition = new MediaComposition();
            composition.Clips.Add(MediaClip.CreateFromColor(global::Windows.UI.Color.FromArgb(255, 42, 82, 120), TimeSpan.FromSeconds(5)));
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
            profile.Audio = null;
            var file = StorageFile.GetFileFromPathAsync(video).AsTask().GetAwaiter().GetResult();
            var result = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile).AsTask().GetAwaiter().GetResult();
            Assert.AreEqual(global::Windows.Media.Transcoding.TranscodeFailureReason.None, result);
        }

        string temp = Path.Combine(isolated, "temp");
        using var app = LaunchApp(ResolveAppExecutablePath(repo), image, Path.Combine(isolated, "data"), temp,
            "en-US", detailsFixture: true);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        WaitForElement(window, automation, "ImageEdit_CommandBar", InteractionTimeout);
        if (windowSize == "maximized") MaximizeWindow(window);
        else if (windowSize == "compact") window.Patterns.Transform.Pattern.Resize(720, 500);
        if (detailsOpen)
        {
            Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
            Element(tabId).Patterns.SelectionItem.Pattern.Select();
        }
        if (populateText)
        {
            Element("ImageEdit_TextExtractionButton").Click();
            var consent = Element("CaptureMemoryConsentDialog");
            WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
            Element("ImageEdit_TextExtractionOverlayMarker");
            Element("CapturePane_Passages");
        }

        try
        {
            var pickerCondition = automation.ConditionFactory.ByName("Open")
                .And(automation.ConditionFactory.ByControlType(ControlType.Window));
            var existingPickers = automation.GetDesktop().FindAllDescendants(pickerCondition)
                .Select(element => element.Properties.NativeWindowHandle.Value).ToHashSet();
            window.Focus();
            WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
            WaitForElementByName(window, automation, "Open...", InteractionTimeout).Click();
            var picker = WaitFor(() => automation.GetDesktop().FindAllDescendants(pickerCondition)
                .FirstOrDefault(element => !existingPickers.Contains(element.Properties.NativeWindowHandle.Value)),
                InteractionTimeout, "Windows file picker");
            var edit = WaitFor(() => picker.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("1148")
                .And(automation.ConditionFactory.ByControlType(ControlType.Edit))), InteractionTimeout, "file name field");
            edit.Focus();
            edit.AsTextBox().Text = video;
            Assert.AreEqual(video, edit.AsTextBox().Text, "The picker contains the intended video path.");
            Keyboard.Type(VirtualKeyShort.RETURN);
            WaitFor(() =>
            {
                Assert.IsTrue(app.IsProcessRunning(), "The editor crashed after File > Open.");
                var trim = window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("VideoEdit_TrimButton"));
                return trim?.IsEnabled == true ? trim : null;
            }, AppLaunchTimeout, "video playback ready");
            Assert.AreEqual(ToggleState.Off, Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.ToggleState.Value);
            WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
            Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
            Assert.IsTrue(Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.IsSelected.Value,
                "Opening a new file must reset the selected tab to File Details.");
            AssertDetailsLayout(window, automation, "VideoEdit_CommandBar", null);
            string screenshot = Path.Combine(isolated, "video-open.png");
            window.CaptureToFile(screenshot);
            TestContext.AddResultFile(screenshot);
        }
        finally
        {
            string log = Path.Combine(temp, "capturetool.log");
            if (File.Exists(log)) TestContext.AddResultFile(log);
        }

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
    }
}

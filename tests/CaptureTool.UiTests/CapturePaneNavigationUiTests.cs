using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow("CapturePane_DetailsTab", "unvisited")]
    [DataRow("CapturePane_TextTab", "unvisited")]
    [DataRow("CapturePane_SummaryTab", "empty")]
    [DataRow("CapturePane_DetailsTab", "empty")]
    [DataRow("CapturePane_TextTab", "empty")]
    [DataRow("CapturePane_SummaryTab", "complete")]
    [DataRow("CapturePane_DetailsTab", "complete")]
    [DataRow("CapturePane_TextTab", "complete")]
    [DataRow("CapturePane_SummaryTab", "running")]
    [TestCategory("UI")]
    public void CapturePane_AboutAndBackReopensEditor(string tabId, string summaryState) =>
        VerifyDetailsNavigation("image", tabId, summaryState);

    [TestMethod]
    [DataRow("video", "CapturePane_SummaryTab")]
    [DataRow("audio", "CapturePane_SummaryTab")]
    [DataRow("video", "CapturePane_DetailsTab")]
    [DataRow("audio", "CapturePane_DetailsTab")]
    [TestCategory("UI")]
    public void CapturePane_RecordingAboutAndBack(string media, string tabId) =>
        VerifyDetailsNavigation(media, tabId, tabId == "CapturePane_SummaryTab" ? "empty" : "unvisited");

    private void VerifyDetailsNavigation(string media, string tabId, string summaryState)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
            "details-navigation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string image = Path.Combine(isolated, "capture.png");
        CreateOcrFixtureImage(image);
        string temp = Path.Combine(isolated, "temp");
        string data = Path.Combine(isolated, "data");
        Directory.CreateDirectory(data);
        if (media != "image")
        {
            string recording = CreateNavigationRecording(isolated, media == "video");
            using var stream = File.Create(Path.Combine(data, "RecentCaptures.json"));
            using var json = new Utf8JsonWriter(stream);
            json.WriteStartArray(); json.WriteStartObject(); json.WriteString("FilePath", recording);
            json.WriteNumber("CaptureFileType", media == "video" ? 1 : 2); json.WriteNumber("Origin", 0);
            json.WriteString("LastActivityUtc", DateTime.UtcNow); json.WriteEndObject(); json.WriteEndArray();
        }
        string commandBar = media switch { "video" => "VideoEdit_CommandBar", "audio" => "AudioEdit_CommandBar", _ => "ImageEdit_CommandBar" };
        using var app = LaunchApp(ResolveAppExecutablePath(repo), image, data, temp,
            "en-US", detailsFixture: true);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        try
        {
            Element("ImageEdit_CommandBar");
            MaximizeWindow(window);
            if (media != "image")
            {
                // Select a tab on an image, then open an editor with fewer available tabs.
                Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
                Element(tabId).Patterns.SelectionItem.Pattern.Select();
                WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
                Element("AppMenu_HomeItem").Patterns.Invoke.Pattern.Invoke();
                var captures = Element("Home_RecentCaptures");
                WaitFor(() => captures.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)),
                    InteractionTimeout, "recording in recent captures").DoubleClick();
                Element(commandBar);
                WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
            }
            for (int visit = 0; visit < 2; visit++)
            {
                Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
                Assert.IsTrue(Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.IsSelected.Value,
                    "Every newly loaded editor must start on File Details.");
                // Also cover leaving the pane without ever instantiating the Summary tab.
                if (summaryState != "unvisited") Element("CapturePane_SummaryTab").Patterns.SelectionItem.Pattern.Select();
                if (visit == 0 && summaryState is "complete" or "running")
                {
                    Element("CaptureAction_Summary").Patterns.Invoke.Pattern.Invoke();
                    var consent = Element("CaptureMemoryConsentDialog");
                    WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
                    WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
                    Element(summaryState == "running" ? "CaptureAction_Summary_Loading" : "CaptureSummary_Result");
                }
                else if (summaryState == "empty") Element("CaptureAction_Summary");
                Element(tabId).Patterns.SelectionItem.Pattern.Select();
                Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
                WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
                Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
                Assert.IsTrue(Element(tabId).Patterns.SelectionItem.Pattern.IsSelected.Value,
                    "Reopening the pane on the same editor must keep its selected tab.");
                WaitForElementByName(window, automation, "Help", InteractionTimeout).Click();
                WaitForElementByName(window, automation, "About", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
                WaitForElementRemoved(window, automation, commandBar, InteractionTimeout);
                WaitForElementByName(window, automation, "Back", InteractionTimeout).AsButton().Invoke();
                WaitFor(() =>
                {
                    Assert.IsTrue(app.IsProcessRunning(), "The editor crashed during Details > About > Back.");
                    return window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(commandBar));
                }, AppLaunchTimeout, "editor after returning from About");
                Assert.AreEqual(ToggleState.Off, Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.ToggleState.Value);
                WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
                if (media == "video")
                    WaitFor(() => Element("VideoEdit_TrimButton").IsEnabled ? window : null, InteractionTimeout, "video playback restored");
                else if (media == "audio") Element("ProgressSlider");
            }
            Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.Toggle();
            Assert.IsTrue(Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.IsSelected.Value,
                "Returning to an editor must start on File Details.");
            Element("CapturePane_SummaryTab").Patterns.SelectionItem.Pattern.Select();
            Element(summaryState is "empty" or "unvisited" ? "CaptureAction_Summary" : "CaptureSummary_Result");
            AssertDetailsLayout(window, automation, commandBar, media switch { "image" => "ZoomSlider", "audio" => "ProgressSlider", _ => null });
        }
        finally
        {
            if (app.IsProcessRunning())
            {
                string screenshot = Path.Combine(isolated, "navigation.png");
                window.CaptureToFile(screenshot);
                TestContext.AddResultFile(screenshot);
            }
            string log = Path.Combine(temp, "capturetool.log");
            if (File.Exists(log)) TestContext.AddResultFile(log);
        }

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
    }

    private static string CreateNavigationRecording(string directory, bool video)
    {
        string path = Path.Combine(directory, video ? "recording.mp4" : "recording.wav");
        if (video)
        {
            File.WriteAllBytes(path, []);
            var composition = new MediaComposition();
            composition.Clips.Add(MediaClip.CreateFromColor(global::Windows.UI.Color.FromArgb(255, 42, 82, 120), TimeSpan.FromSeconds(5)));
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
            profile.Audio = null;
            var file = StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult();
            var result = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile).AsTask().GetAwaiter().GetResult();
            Assert.AreEqual(global::Windows.Media.Transcoding.TranscodeFailureReason.None, result);
        }
        else
        {
            using var writer = new BinaryWriter(File.Create(path));
            const int bytes = 48000 * 2 * 5;
            writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000);
            writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
        return path;
    }
}

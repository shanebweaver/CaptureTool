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
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("UI")]
    public void CapturePane_RecordingTextAndLocations(bool video)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests to run this check.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "capture-pane-media", video ? "video" : "audio");
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        string data = Path.Combine(isolated, "data"), temp = Path.Combine(isolated, "temp");
        Directory.CreateDirectory(data); Directory.CreateDirectory(temp);
        string image = Path.Combine(isolated, "startup.png");
        CreateOcrFixtureImage(image);
        string media = Path.Combine(isolated, video ? "recording.mp4" : "recording.wav");
        if (video)
        {
            File.WriteAllBytes(media, []);
            var composition = new MediaComposition();
            composition.Clips.Add(MediaClip.CreateFromColor(global::Windows.UI.Color.FromArgb(255, 42, 82, 120), TimeSpan.FromSeconds(5)));
            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
            profile.Audio = null;
            var file = StorageFile.GetFileFromPathAsync(media).AsTask().GetAwaiter().GetResult();
            var result = composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile).AsTask().GetAwaiter().GetResult();
            Assert.AreEqual(global::Windows.Media.Transcoding.TranscodeFailureReason.None, result);
        }
        else
        {
            using var writer = new BinaryWriter(File.Create(media));
            const int bytes = 48000 * 2 * 5;
            writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(48000);
            writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(bytes); writer.Write(new byte[bytes]);
        }
        using (var stream = File.Create(Path.Combine(data, "RecentCaptures.json")))
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartArray(); json.WriteStartObject(); json.WriteString("FilePath", media);
            json.WriteNumber("CaptureFileType", video ? 1 : 2); json.WriteNumber("Origin", 0);
            json.WriteString("LastActivityUtc", DateTime.UtcNow); json.WriteEndObject(); json.WriteEndArray();
        }
        using var app = LaunchApp(ResolveAppExecutablePath(repo), image, data, temp, "en-US", detailsFixture: true);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
        MaximizeWindow(window);
        Menu("AppMenu_HomeItem"); OpenRecording(); OpenPane();
        var pane = Element("CaptureDetailsPane");
        WaitForElementByName(pane, automation, video ? "Video" : "Audio", InteractionTimeout);
        AssertDetailsLayout(window, automation, video ? "VideoEdit_CommandBar" : "AudioEdit_CommandBar",
            video ? null : "ProgressSlider");
        Element("Editor_DetailsToggle").Click();
        WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
        Assert.AreEqual(ToggleState.Off, Element("Editor_DetailsToggle").Patterns.Toggle.Pattern.ToggleState.Value);
        Element("Editor_DetailsToggle").Click();
        AssertDetailsLayout(window, automation, video ? "VideoEdit_CommandBar" : "AudioEdit_CommandBar",
            video ? null : "ProgressSlider");
        window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
        window.Patterns.Transform.Pattern.Resize(720, 760);
        Thread.Sleep(350);
        AssertDetailsLayout(window, automation, video ? "VideoEdit_CommandBar" : "AudioEdit_CommandBar",
            video ? null : "ProgressSlider");
        string compact = Path.Combine(artifacts, "compact-details.png");
        window.CaptureToFile(compact); TestContext.AddResultFile(compact);
        MaximizeWindow(window);
        if (video)
        {
            Element("VideoEdit_TrimButton").Patterns.Toggle.Pattern.Toggle();
            WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
            Assert.IsFalse(Element("VideoEdit_TrimStartTime").IsOffscreen);
            OpenPane();
            Assert.AreEqual(ToggleState.Off, Element("VideoEdit_TrimButton").Patterns.Toggle.Pattern.ToggleState.Value);
        }
        window.CaptureToFile(Path.Combine(artifacts, "local-details.png"));
        Element("CapturePane_Close").Patterns.Invoke.Pattern.Invoke();
        Thread.Sleep(350);
        Menu("AppMenu_SettingsItem");
        Element("CaptureMemoryConsent").Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "Allow local AI");
        // Read exact localized dialog labels from the resources rather than depending on the capture fixture.
        var resources = System.Xml.Linq.XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", "en-US", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(item => (string)item.Attribute("name")!, item => item.Element("value")!.Value);
        Menu("AppMenu_HomeItem"); OpenRecording(); OpenPane();
        Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
        Element("CaptureAction_Transcript").Patterns.Invoke.Pattern.Invoke();
        var search = Element("CapturePane_Search").AsTextBox();
        search.Text = "spoken";
        WaitForElementByName(window, automation, "Second spoken passage", TimeSpan.FromSeconds(45));
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CapturePane_SourceFilter")),
            "A recording with only speech results should not show a source selector.");
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard() == "First spoken passage" + Environment.NewLine + "Second spoken passage" ? window : null,
            InteractionTimeout, "complete filtered transcript");
        WaitForElementByName(Element("CapturePane_Passages"), automation, "Second spoken passage", InteractionTimeout).Click();
        WaitFor(() =>
        {
            var progress = Element("ProgressSlider").Patterns.RangeValue.Pattern;
            double fraction = (progress.Value.Value - progress.Minimum.Value) / (progress.Maximum.Value - progress.Minimum.Value);
            return Math.Abs(fraction - .6) < .02 ? window : null;
        },
            InteractionTimeout, "clicking a transcript row seeks to its timestamp");
        Thread.Sleep(250);
        string screenshot = Path.Combine(artifacts, "transcript-location.png");
        window.CaptureToFile(screenshot); TestContext.AddResultFile(screenshot);
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByName(resources["CapturePane_ActionFailed"])));

        Element("CapturePane_DetailsTab").Patterns.SelectionItem.Pattern.Select();
        Element("CaptureName_Edit").Patterns.Invoke.Pattern.Invoke();
        Element("CaptureName_Input").AsTextBox().Text = "Accepted recording";
        Element("CaptureName_Save").Patterns.Invoke.Pattern.Invoke();
        string renamed = Path.Combine(isolated, "Accepted recording" + Path.GetExtension(media));
        WaitFor(() => File.Exists(renamed) && !File.Exists(media) ? window : null, InteractionTimeout, "rename an open recording");
        WaitFor(() => Element("CaptureDetailsFileName").Name == Path.GetFileName(renamed) ? window : null,
            InteractionTimeout, "recording editor tracks the renamed file");

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void Menu(string id)
        {
            window.Focus(); WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
            Element(id).Patterns.Invoke.Pattern.Invoke();
        }
        void OpenRecording()
        {
            var grid = Element("Home_RecentCaptures");
            WaitFor(() => grid.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.ListItem)), InteractionTimeout, "recording").DoubleClick();
            Element("Editor_DetailsToggle");
        }
        void OpenPane()
        {
            var toggle = Element("Editor_DetailsToggle").Patterns.Toggle.Pattern;
            if (toggle.ToggleState.Value != ToggleState.On) toggle.Toggle();
            Thread.Sleep(350);
        }
        void Confirm(string prompt, string answer)
        {
            var dialog = Element("CaptureMemory" + prompt + "Dialog");
            WaitForElementByName(dialog, automation, answer, InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemory" + prompt + "Dialog", InteractionTimeout);
        }
    }
}

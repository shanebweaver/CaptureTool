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
        MaximizeWindow(window);
        WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
        Menu("AppMenu_HomeItem"); OpenRecording(); OpenPane();
        var pane = Element("CaptureDetailsPane");
        WaitForElementByName(pane, automation, video ? "Video" : "Audio", InteractionTimeout);
        window.CaptureToFile(Path.Combine(artifacts, "local-details.png"));
        Menu("AppMenu_SettingsItem");
        Element("CaptureMemoryScanning").Patterns.Toggle.Pattern.Toggle();
        Confirm("Consent", "Allow");
        // Read exact localized dialog labels from the resources rather than depending on the capture fixture.
        var resources = System.Xml.Linq.XDocument.Load(Path.Combine(repo, "src", "CaptureTool.Presentation.Windows.WinUI", "Strings", "en-US", "Resources.resw"))
            .Root!.Elements("data").ToDictionary(item => (string)item.Attribute("name")!, item => item.Element("value")!.Value);
        Confirm("ScanExisting", resources["CaptureMemory_ScanExistingAccept"]);
        Menu("AppMenu_HomeItem"); OpenRecording(); OpenPane();
        Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
        var search = Element("CapturePane_Search").AsTextBox();
        search.Text = "spoken";
        WaitForElementByName(window, automation, "Second spoken passage", TimeSpan.FromSeconds(45));
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard() == "First spoken passage" + Environment.NewLine + "Second spoken passage" ? window : null,
            InteractionTimeout, "complete filtered transcript");
        var location = WaitFor(() => window.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Button).And(automation.ConditionFactory.ByName(resources["CaptureDetails_Transcript"] + " 0:03")))
            .FirstOrDefault(item => item.IsEnabled), InteractionTimeout, "recording location");
        location.Patterns.Invoke.Pattern.Invoke();
        Thread.Sleep(250);
        string screenshot = Path.Combine(artifacts, "transcript-location.png");
        window.CaptureToFile(screenshot); TestContext.AddResultFile(screenshot);
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByName(resources["CapturePane_ActionFailed"])));

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

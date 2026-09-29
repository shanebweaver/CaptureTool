using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("UI")]
    public void TextTab_ShowsParagraphsOrEmptyStateAndReusesCompletedScan(bool emptyScan)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string source = Path.Combine(repo, "tests", "Fixtures", "TextLayout");
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "text-layout");
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string image = Path.Combine(isolated, "text-layout.png");
        File.Copy(Path.Combine(source, "text-layout.png"), image);
        string fixture = Path.Combine(source, "text-layout.json");
        if (emptyScan)
        {
            using var bitmap = new System.Drawing.Bitmap(480, 320);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.Clear(System.Drawing.Color.White);
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
            fixture = Path.Combine(isolated, "empty-text.json");
            File.WriteAllText(fixture, """{"Width":480,"Height":320,"Words":[],"Paragraphs":[]}""");
        }
        using var document = JsonDocument.Parse(File.ReadAllText(fixture));
        string[] paragraphs = document.RootElement.GetProperty("Paragraphs").EnumerateArray()
            .Select(value => value.GetString()!.Replace("\n", Environment.NewLine)).ToArray();
        string expectedCopy = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        using var app = LaunchApp(ResolveAppExecutablePath(repo), image, Path.Combine(isolated, "data"),
            Path.Combine(isolated, "temp"), "en-US", detailsFixture: true, textFixturePath: fixture);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        Element("ImageEdit_CommandBar"); MaximizeWindow(window);
        Element("Editor_DetailsToggle").Click();
        Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
        var scanBounds = StableScanBounds();
        string scanLabel = Element("CaptureAction_ScanText").Name;
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CapturePane_Search")));
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CapturePane_EmptyTextTitle")));
        Screenshot("before-scan");
        Element("CaptureAction_ScanText").Patterns.Invoke.Pattern.Invoke();
        var consent = Element("CaptureMemoryConsentDialog");
        WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
        WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
        var loading = Element("CaptureAction_ScanText_Loading");
        Assert.IsGreaterThanOrEqualTo(scanBounds.Bottom, loading.BoundingRectangle.Top, "Progress belongs below the action.");
        Assert.IsFalse(scanBounds.IntersectsWith(loading.BoundingRectangle));
        Assert.AreEqual(scanBounds, StableScanBounds(), "Loading must not resize or move the button.");
        Assert.AreEqual(scanLabel, Element("CaptureAction_ScanText").Name);
        Assert.IsFalse(Element("CaptureAction_ScanText").IsEnabled);
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CapturePane_Search")));
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CapturePane_EmptyTextTitle")));
        Screenshot("action-loading");
        WaitForElementRemoved(window, automation, "CaptureAction_ScanText_Loading", InteractionTimeout);
        WaitForElementRemoved(window, automation, "CaptureAction_ScanText", InteractionTimeout);
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByName("No QR codes found.")),
            "Scans without QR codes should complete without a notification.");
        if (emptyScan)
        {
            AssertEmptyState();
            Screenshot("results");
            Element("CapturePane_Close").Patterns.Invoke.Pattern.Invoke();
            Element("ImageEdit_TextExtractionButton").Click();
            AssertEmptyState();
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureAction_ScanText_Loading")));
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureAction_ScanText")));
            Element("ImageEdit_RotateButton").Patterns.Invoke.Pattern.Invoke();
            Element("CaptureAction_ScanText");
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CapturePane_EmptyTextTitle")),
                "Editing the image should clear the completed empty scan.");
            return;
        }
        var results = Element("CapturePane_Search");
        Assert.AreEqual(scanBounds.Top, results.BoundingRectangle.Top, "Results replace the action at the top of the tab.");
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureAction_Qr")));
        Element("ImageEdit_TextExtractionOverlayMarker");
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard()?.Contains("Field notes") == true ? window : null, InteractionTimeout, "complete fixture text copied");
        Assert.AreEqual(expectedCopy, ReadDetailsClipboard(), "Paragraphs should read down each column, with blank lines between groups.");
        Screenshot("all-paragraphs");

        CheckPassage("Morning light", paragraphs[3], "paragraph-selection");
        CheckPassage("Later,", paragraphs[5], "indented-paragraph-selection");
        CheckPassage("3. Keep", paragraphs[9], "wrapped-list-selection");
        Element("CapturePane_Search").AsTextBox().Text = string.Empty;
        Element("CapturePane_Close").Patterns.Invoke.Pattern.Invoke();
        Element("ImageEdit_TextExtractionButton").Click();
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureAction_ScanText_Loading")));
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureAction_ScanText")));
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard() == expectedCopy ? window : null, InteractionTimeout, "same paragraph layout after reopening");

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        void AssertEmptyState()
        {
            Assert.AreEqual("No results found", Element("CapturePane_EmptyTextTitle").Name);
            WaitForElementByName(Element("CaptureDetailsPane"), automation,
                "No text or QR codes were found in this capture.", InteractionTimeout);
            foreach (string id in new[] { "CapturePane_Search", "CapturePane_MatchCount", "CapturePane_Passages", "CapturePane_CopyResults" })
                Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(id)), "Empty scans have no results controls.");
            Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByName("No text found.")));
        }
        System.Drawing.Rectangle StableScanBounds()
        {
            var bounds = Element("CaptureAction_ScanText").BoundingRectangle;
            var stable = System.Diagnostics.Stopwatch.StartNew();
            return WaitFor(() =>
            {
                var button = Element("CaptureAction_ScanText");
                if (button.BoundingRectangle != bounds) { bounds = button.BoundingRectangle; stable.Restart(); }
                return stable.Elapsed >= TimeSpan.FromMilliseconds(500) ? button : null;
            }, InteractionTimeout, "scan button geometry after pane, Pivot, and consent transitions").BoundingRectangle;
        }
        void CheckPassage(string query, string expected, string screenshot)
        {
            Element("CapturePane_Search").AsTextBox().Text = query;
            var text = WaitForElementByName(Element("CapturePane_Passages"), automation, expected, InteractionTimeout);
            text.Click();
            WaitFor(() => Element("CapturePane_Passages").FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .FirstOrDefault(row => row.Patterns.SelectionItem.Pattern.IsSelected.Value), InteractionTimeout, "passage row selected");
            Screenshot(screenshot);
        }
        void Screenshot(string name)
        {
            Thread.Sleep(250);
            string path = Path.Combine(artifacts, (emptyScan ? "empty-" : string.Empty) + name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }
}

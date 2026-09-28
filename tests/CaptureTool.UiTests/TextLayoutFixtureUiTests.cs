using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [TestCategory("UI")]
    public void TextLayoutFixture_GroupsParagraphsAndSelectsWrappedPassages()
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
        using var document = JsonDocument.Parse(File.ReadAllText(fixture));
        string[] paragraphs = document.RootElement.GetProperty("Paragraphs").EnumerateArray()
            .Select(value => value.GetString()!.Replace("\n", Environment.NewLine)).ToArray();
        string expectedCopy = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        using var app = LaunchApp(ResolveAppExecutablePath(repo), image, Path.Combine(isolated, "data"),
            Path.Combine(isolated, "temp"), "en-US", detailsFixture: true, textFixturePath: fixture);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        Element("ImageEdit_CommandBar"); MaximizeWindow(window);
        Element("ImageEdit_TextExtractionButton").Click();
        var consent = Element("CaptureMemoryConsentDialog");
        WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
        WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
        WaitForElementRemoved(window, automation, "CaptureAction_Text", InteractionTimeout);
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
        Assert.IsNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("CaptureAction_Text_Loading")));
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard() == expectedCopy ? window : null, InteractionTimeout, "same paragraph layout after reopening");

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
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
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }
}

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using ZXing;
using ZXing.Common;

namespace CaptureTool.UiTests;

[TestClass]
public sealed partial class ImageEditTextExtractionUiTests
{
    private static readonly TimeSpan AppLaunchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InteractionTimeout = TimeSpan.FromSeconds(15);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestCategory("UI")]
    public void ExtractTextShortcut_UnifiesSavedAndEditedImageTextInThePane(bool editBeforeConsent)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string artifacts = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts", "unified-text",
            editBeforeConsent ? "edited-first-use" : "saved-first-use");
        string isolated = Path.Combine(artifacts, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string fixture = Path.Combine(isolated, "capture.png");
        CreateOcrFixtureImage(fixture);
        using var app = LaunchApp(ResolveAppExecutablePath(repo), fixture, Path.Combine(isolated, "data"),
            Path.Combine(isolated, "temp"), "en-US", detailsFixture: true);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
        Element("ImageEdit_CommandBar"); MaximizeWindow(window);
        if (editBeforeConsent) Element("ImageEdit_RotateButton").Patterns.Invoke.Pattern.Invoke();
        Element("Editor_DetailsToggle").Click();
        Element("CapturePane_TextTab").Patterns.SelectionItem.Pattern.Select();
        Assert.IsNull(Find("CaptureMemoryConsentDialog"), "Browsing the pane does not request analysis.");
        Assert.IsNull(Find("CaptureAction_ScanText_Loading"));
        var shortcut = Element("ImageEdit_TextExtractionButton");
        Assert.IsFalse(shortcut.Patterns.Toggle.IsSupported, "Extract text is a shortcut, not a second mode.");
        shortcut.Click();
        var consent = Element("CaptureMemoryConsentDialog");
        WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
        WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
        Element("CaptureAction_ScanText_Loading");
        Assert.IsNull(Find("ImageEdit_TextExtractionProgressRing"));
        AssertDetailsLayout(window, automation, "ImageEdit_CommandBar", "ZoomSlider");
        Screenshot("loading");
        WaitForElementRemoved(window, automation, "CaptureAction_ScanText_Loading", InteractionTimeout);
        Element("ImageEdit_TextExtractionOverlayMarker");
        Assert.IsNull(Find("ImageEdit_TextExtractionCopyAllButton"));
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard()?.Contains(editBeforeConsent ? "OCR MODE" : "Contoso invoice") == true ? window : null,
            InteractionTimeout, "text copied from the pane after first-use consent");
        shortcut.Click();
        Assert.IsNull(Find("CaptureAction_ScanText_Loading"));
        Element("CapturePane_SummaryTab").Patterns.SelectionItem.Pattern.Select();
        WaitForElementRemoved(window, automation, "ImageEdit_TextExtractionOverlayMarker", InteractionTimeout);
        shortcut.Click();
        Element("ImageEdit_TextExtractionOverlayMarker");
        Assert.IsNull(Find("CaptureAction_ScanText_Loading"));
        Screenshot("saved-text");

        Element("ImageEdit_RotateButton").Patterns.Invoke.Pattern.Invoke();
        Element("CaptureAction_ScanText");
        WaitForElementRemoved(window, automation, "ImageEdit_TextExtractionOverlayMarker", InteractionTimeout);
        Assert.IsNull(Find("CapturePane_Search"), "Saved text is hidden once the image changes.");
        shortcut.Click();
        Element("CaptureAction_ScanText_Loading");
        Element("CapturePane_Close").Patterns.Invoke.Pattern.Invoke();
        WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
        Element("Editor_DetailsToggle").Click();
        Element("CaptureAction_ScanText");
        WaitForElementRemoved(window, automation, "CaptureAction_ScanText_Loading", InteractionTimeout);
        Assert.IsNull(Find("ImageEdit_TextExtractionOverlayMarker"), "Closing the pane cancels current-image extraction.");
        shortcut.Click();
        Element("CaptureAction_ScanText_Loading");
        WaitForElementRemoved(window, automation, "CaptureAction_ScanText_Loading", InteractionTimeout);
        Element("ImageEdit_TextExtractionOverlayMarker");
        Element("ImageCanvas_QrCodeCopyButton_0");
        Element("CapturePane_CopyResults").Patterns.Invoke.Pattern.Invoke();
        WaitFor(() => ReadDetailsClipboard()?.Contains("OCR MODE") == true && ReadDetailsClipboard()?.Contains("https://example.com/capturetool") == true
            ? window : null, InteractionTimeout, "current rendered image text and QR results copied");
        var passageText = WaitFor(() => Element("CapturePane_Passages").FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.Text))
            .FirstOrDefault(item => !item.IsOffscreen && item.Name.Contains("OCR MODE")), InteractionTimeout, "current-image passages visibly rendered");
        Assert.IsGreaterThan(0d, passageText.BoundingRectangle.Height);
        Screenshot("edited-image-text");
        passageText.Click();
        WaitFor(() => Element("CapturePane_Passages").FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.ListItem))
            .FirstOrDefault(item => item.Patterns.SelectionItem.Pattern.IsSelected.Value), InteractionTimeout, "current-image row selected by clicking its text");
        Screenshot("edited-image-selection");
        Element("CapturePane_Close").Patterns.Invoke.Pattern.Invoke();
        WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
        shortcut.Click();
        Element("ImageEdit_TextExtractionOverlayMarker");
        Assert.IsNull(Find("CaptureAction_ScanText_Loading"), "Reopening reuses current-session extraction.");
        Element("ImageEdit_CropButton").Click();
        WaitForElementRemoved(window, automation, "CaptureDetailsPane", InteractionTimeout);
        WaitForElementRemoved(window, automation, "ImageEdit_TextExtractionOverlayMarker", InteractionTimeout);

        AutomationElement Element(string id) => WaitForElement(window, automation, id, InteractionTimeout);
        AutomationElement? Find(string id) => window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(id));
        void Screenshot(string name)
        {
            Thread.Sleep(200);
            string path = Path.Combine(artifacts, name + ".png");
            window.CaptureToFile(path); TestContext.AddResultFile(path);
        }
    }
    private static void RequireUiTestLanguage(string language)
    {
        string? requested = Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_LANGUAGE");
        requested = string.IsNullOrWhiteSpace(requested) ? "en-US" : requested;
        if (!string.Equals(requested, "all", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(requested, language, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("UI tests default to en-US. Set CAPTURETOOL_UI_TEST_LANGUAGE to select another language or explicitly request all.");
        }
    }

    private static bool ShouldRunUiTests()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("CAPTURETOOL_RUN_UI_TESTS"),
            "1",
            StringComparison.OrdinalIgnoreCase);
    }

    private static LaunchedCaptureToolApp LaunchApp(
        string appExecutablePath,
        string fixtureImagePath,
        string appDataDirectory,
        string appTempDirectory,
        string? language = null,
        bool detailsFixture = false, bool captureFixture = false, bool onboarding = false, string? textFixturePath = null)
    {
        string[] appArguments = [
            "--capturetool-ui-test",
            "--ui-test-image",
            fixtureImagePath,
            "--ui-test-data-dir",
            appDataDirectory,
            "--ui-test-temp-dir",
            appTempDirectory
        ];

        ProcessStartInfo startInfo = new(appExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(appExecutablePath)
        };

        foreach (string argument in appArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (onboarding) startInfo.ArgumentList.Add("--ui-test-onboarding");
        if (detailsFixture) startInfo.ArgumentList.Add("--ui-test-details");
        if (captureFixture) startInfo.ArgumentList.Add("--ui-test-capture");
        if (textFixturePath != null)
        {
            startInfo.ArgumentList.Add("--ui-test-text-fixture");
            startInfo.ArgumentList.Add(textFixturePath);
        }
        if (language != null)
        {
            startInfo.ArgumentList.Add("--ui-test-language");
            startInfo.ArgumentList.Add(language);
        }

        Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to launch CaptureTool.");

        return new LaunchedCaptureToolApp(process);
    }

    private static Window WaitForMainWindow(
        LaunchedCaptureToolApp app,
        UIA3Automation automation,
        TimeSpan timeout)
    {
        return WaitFor(
            () =>
            {
                if (!app.IsProcessRunning())
                {
                    Assert.Fail("CaptureTool exited before the main window appeared.");
                }

                try
                {
                    return automation
                        .GetDesktop()
                        .FindAllChildren(automation.ConditionFactory.ByProcessId(app.ProcessId))
                        .Select(element => element.AsWindow())
                        .FirstOrDefault(window =>
                            window.ControlType == ControlType.Window &&
                            window.BoundingRectangle.Width > 0 &&
                            window.BoundingRectangle.Height > 0);
                }
                catch
                {
                    return null;
                }
            },
            timeout,
            "main window");
    }

    private static AutomationElement WaitForElement(
        AutomationElement root,
        UIA3Automation automation,
        string automationId,
        TimeSpan timeout)
    {
        try
        {
            return WaitFor(
                () => root.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(automationId)),
                timeout,
                $"element with AutomationId '{automationId}'");
        }
        catch (AssertFailedException ex)
        {
            Assert.Fail($"{ex.Message}{Environment.NewLine}{DescribeAutomationTree(root, automation)}");
            throw new UnreachableException();
        }
    }

    private static AutomationElement WaitForElementByName(
        AutomationElement root,
        UIA3Automation automation,
        string name,
        TimeSpan timeout)
    {
        return WaitFor(
            () => root.FindFirstDescendant(automation.ConditionFactory.ByName(name)),
            timeout,
            $"element named '{name}'");
    }

    private static void MaximizeWindow(Window window)
    {
        if (window.Patterns.Window.IsSupported)
        {
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            Thread.Sleep(250);
        }
    }

    private static void WaitForElementRemoved(
        AutomationElement root,
        UIA3Automation automation,
        string automationId,
        TimeSpan timeout)
    {
        WaitFor(
            () => root.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(automationId)) is null
                ? new object()
                : null,
            timeout,
            $"element with AutomationId '{automationId}' to be removed");
    }

    private static T WaitFor<T>(
        Func<T?> getValue,
        TimeSpan timeout,
        string description)
        where T : class
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                T? value = getValue();
                if (value is not null)
                {
                    return value;
                }
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            Thread.Sleep(200);
        }

        string message = $"Timed out waiting for {description}.";
        if (lastException is not null)
        {
            message += $" Last exception: {lastException.Message}";
        }

        Assert.Fail(message);
        throw new UnreachableException();
    }

    private static void CaptureWindowScreenshot(
        int processId,
        AutomationElement fallbackElement,
        string filePath)
    {
        Rectangle bounds = GetMainWindowBounds(processId) ?? GetElementBounds(fallbackElement);
        Assert.IsGreaterThan(0, bounds.Width, "The captured window width should be greater than zero.");
        Assert.IsGreaterThan(0, bounds.Height, "The captured window height should be greater than zero.");

        using Bitmap bitmap = new(bounds.Width, bounds.Height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        bitmap.Save(filePath, ImageFormat.Png);
    }

    private static Rectangle? GetMainWindowBounds(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            nint handle = process.MainWindowHandle;
            if (handle == 0 || !GetWindowRect(handle, out WindowRect rect))
            {
                return null;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            return width > 0 && height > 0
                ? new Rectangle(rect.Left, rect.Top, width, height)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static Rectangle GetElementBounds(AutomationElement element)
    {
        var bounds = element.BoundingRectangle;
        Assert.IsGreaterThan(0, bounds.Width, "The fallback element width should be greater than zero.");
        Assert.IsGreaterThan(0, bounds.Height, "The fallback element height should be greater than zero.");

        int left = (int)Math.Floor((double)bounds.Left);
        int top = (int)Math.Floor((double)bounds.Top);
        int width = (int)Math.Ceiling((double)bounds.Width);
        int height = (int)Math.Ceiling((double)bounds.Height);

        return new Rectangle(left, top, width, height);
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out WindowRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct WindowRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }

    private static string DescribeAutomationTree(
        AutomationElement root,
        UIA3Automation automation)
    {
        StringBuilder builder = new();
        builder.AppendLine("Automation tree snapshot:");
        AppendAutomationTree(builder, root, automation, 0, 3, 80);
        return builder.ToString();
    }

    private static int AppendAutomationTree(
        StringBuilder builder,
        AutomationElement element,
        UIA3Automation automation,
        int depth,
        int maxDepth,
        int remaining)
    {
        if (remaining <= 0)
        {
            return 0;
        }

        string indent = new(' ', depth * 2);
        builder
            .Append(indent)
            .Append(GetPropertyValue(() => element.ControlType.ToString()))
            .Append(" Id='")
            .Append(GetPropertyValue(() => element.AutomationId))
            .Append("' Name='")
            .Append(GetPropertyValue(() => element.Name))
            .AppendLine("'");

        remaining--;
        if (depth >= maxDepth)
        {
            return remaining;
        }

        AutomationElement[] children;
        try
        {
            children = element.FindAllChildren();
        }
        catch
        {
            return remaining;
        }

        foreach (AutomationElement child in children)
        {
            remaining = AppendAutomationTree(builder, child, automation, depth + 1, maxDepth, remaining);
            if (remaining <= 0)
            {
                break;
            }
        }

        return remaining;
    }

    private static string GetPropertyValue(Func<string?> getValue)
    {
        try
        {
            return getValue() ?? string.Empty;
        }
        catch
        {
            return "<unsupported>";
        }
    }

    private static void CreateOcrFixtureImage(string filePath)
    {
        using Bitmap bitmap = new(640, 300);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.White);

        using Pen guidePen = new(Color.FromArgb(220, 30, 118, 210), 3);
        using Brush titleBrush = new SolidBrush(Color.FromArgb(20, 20, 20));
        using Brush subtitleBrush = new SolidBrush(Color.FromArgb(40, 40, 40));
        using Font titleFont = new("Segoe UI", 34, FontStyle.Bold, GraphicsUnit.Pixel);
        using Font subtitleFont = new("Segoe UI", 30, FontStyle.Regular, GraphicsUnit.Pixel);

        graphics.DrawRectangle(guidePen, 24, 24, 592, 252);
        graphics.DrawString("OCR MODE", titleFont, titleBrush, 50, 40);
        graphics.DrawString("SAMPLE TEXT", subtitleFont, subtitleBrush, 50, 110);

        var qrWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Width = 190,
                Height = 190,
                Margin = 3
            }
        };
        ZXing.Rendering.PixelData qrPixels = qrWriter.Write("https://example.com/capturetool");
        using Bitmap qrBitmap = new(qrPixels.Width, qrPixels.Height, PixelFormat.Format32bppArgb);
        BitmapData qrData = qrBitmap.LockBits(
            new Rectangle(0, 0, qrBitmap.Width, qrBitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(qrPixels.Pixels, 0, qrData.Scan0, qrPixels.Pixels.Length);
        }
        finally
        {
            qrBitmap.UnlockBits(qrData);
        }

        graphics.DrawImage(qrBitmap, 400, 55, 190, 190);

        bitmap.Save(filePath, ImageFormat.Png);
    }

    private sealed class LaunchedCaptureToolApp : IDisposable
    {
        public LaunchedCaptureToolApp(Process process)
        {
            ProcessId = process.Id;
            process.Dispose();
        }

        public int ProcessId { get; }

        public bool IsProcessRunning()
        {
            try
            {
                using Process process = Process.GetProcessById(ProcessId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public void Close()
        {
            try
            {
                using Process process = Process.GetProcessById(ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

    private static string ResolveAppExecutablePath(string repoRoot)
    {
        if (Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_APP_PATH") is { Length: > 0 } publishedApp)
            return Path.GetFullPath(publishedApp);

        string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
        string platform = Environment.GetEnvironmentVariable("PLATFORM") ?? "x64";
        string runtimeIdentifier = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 || platform.Equals("ARM64", StringComparison.OrdinalIgnoreCase)
            ? "win-arm64"
            : "win-x64";

        return Path.Combine(
            repoRoot,
            "src",
            "CaptureTool.Presentation.Windows.WinUI",
            "bin",
            platform,
            configuration,
            "net10.0-windows10.0.26100.0",
            runtimeIdentifier,
            "CaptureTool.Presentation.Windows.WinUI.exe");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CaptureTool.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}

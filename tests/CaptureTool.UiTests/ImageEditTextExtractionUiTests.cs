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
public sealed class ImageEditTextExtractionUiTests
{
    private static readonly TimeSpan AppLaunchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InteractionTimeout = TimeSpan.FromSeconds(15);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("UI")]
    public void TextExtractionMode_ShouldShowOverlayAndCaptureScreenshot()
    {
        if (!ShouldRunUiTests())
        {
            Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        }

        string repoRoot = FindRepositoryRoot();
        string appExecutablePath = ResolveAppExecutablePath(repoRoot);
        Assert.IsFalse(string.IsNullOrWhiteSpace(appExecutablePath));
        Assert.IsTrue(File.Exists(appExecutablePath), $"The WinUI app should be built at {appExecutablePath}.");

        string artifactDirectory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? TestContext.TestRunDirectory ?? Path.GetTempPath(),
            "CaptureTool.UiTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactDirectory);

        string fixtureImagePath = Path.Combine(artifactDirectory, "ocr-fixture.png");
        string appDataDirectory = Path.Combine(artifactDirectory, "app-data");
        string appTempDirectory = Path.Combine(artifactDirectory, "app-temp");
        string screenshotDirectory = Path.Combine(repoRoot, "tests", "CaptureTool.UiTests", "TestResults", "artifacts");
        string loadingScreenshotPath = Path.Combine(screenshotDirectory, "text-extraction-loading.png");
        string screenshotPath = Path.Combine(screenshotDirectory, "text-extraction-overlay.png");

        CreateOcrFixtureImage(fixtureImagePath);
        Directory.CreateDirectory(appDataDirectory);
        Directory.CreateDirectory(appTempDirectory);
        Directory.CreateDirectory(screenshotDirectory);

        using LaunchedCaptureToolApp app = LaunchApp(
            appExecutablePath,
            fixtureImagePath,
            appDataDirectory,
            appTempDirectory);

        try
        {
            using var automation = new UIA3Automation();
            Window mainWindow = WaitForMainWindow(app, automation, AppLaunchTimeout);
            mainWindow.Focus();
            MaximizeWindow(mainWindow);

            WaitForElement(mainWindow, automation, "ImageEdit_CommandBar", AppLaunchTimeout);

            AutomationElement textExtractionButton = WaitForElement(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionButton",
                InteractionTimeout);
            textExtractionButton.Click();

            WaitForElement(
                mainWindow,
                automation,
                "AiFeatureConsentDialog",
                InteractionTimeout);
            AutomationElement allowButton = WaitForElementByName(
                mainWindow,
                automation,
                "Allow",
                InteractionTimeout);
            allowButton.Click();

            WaitForElement(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionProgressRing",
                InteractionTimeout);

            CaptureWindowScreenshot(app.ProcessId, mainWindow, loadingScreenshotPath);
            Assert.IsTrue(File.Exists(loadingScreenshotPath), "The OCR loading screenshot should exist.");
            Assert.IsGreaterThan(0L, new FileInfo(loadingScreenshotPath).Length, "The OCR loading screenshot should not be empty.");
            TestContext.AddResultFile(loadingScreenshotPath);
            TestContext.WriteLine($"Text Extraction loading screenshot: {loadingScreenshotPath}");

            WaitForElementRemoved(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionProgressRing",
                InteractionTimeout);

            WaitForElement(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionOverlayMarker",
                InteractionTimeout);
            AutomationElement copyAllTextButton = WaitForElement(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionCopyAllButton",
                InteractionTimeout);
            Assert.IsTrue(copyAllTextButton.IsEnabled, "Copy all text should be enabled after OCR completes.");
            WaitForElement(
                mainWindow,
                automation,
                "ImageCanvas_QrCodeCopyButton_0",
                InteractionTimeout);
            WaitForElement(
                mainWindow,
                automation,
                "ImageCanvas_QrCodeOpenButton_0",
                InteractionTimeout);

            Thread.Sleep(500);
            File.Delete(screenshotPath);
            CaptureWindowScreenshot(app.ProcessId, mainWindow, screenshotPath);

            Assert.IsTrue(File.Exists(screenshotPath), "The OCR overlay screenshot should exist.");
            Assert.IsGreaterThan(0L, new FileInfo(screenshotPath).Length, "The OCR overlay screenshot should not be empty.");
            TestContext.AddResultFile(screenshotPath);
            TestContext.WriteLine($"Text Extraction overlay screenshot: {screenshotPath}");

            textExtractionButton.Click();
            WaitForElementRemoved(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionOverlayMarker",
                InteractionTimeout);

            textExtractionButton.Click();
            Assert.IsNull(
                mainWindow.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId("ImageEdit_TextExtractionProgressRing")),
                "Reopening OCR for an unchanged image should reuse its cached result without showing the loader.");
            WaitForElement(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionOverlayMarker",
                InteractionTimeout);

            textExtractionButton.Click();
            WaitForElementRemoved(
                mainWindow,
                automation,
                "ImageEdit_TextExtractionOverlayMarker",
                InteractionTimeout);
        }
        finally
        {
            app.Close();
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
        string appTempDirectory)
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

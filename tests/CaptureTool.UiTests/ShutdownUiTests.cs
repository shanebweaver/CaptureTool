using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow(false, "shutdown-cancel")]
    [DataRow(true, "shutdown-cancel")]
    [DataRow(false, "shutdown-late-result")]
    [DataRow(true, "shutdown-late-result")]
    [TestCategory("UI")]
    public void Shutdown_DrainsRunningAiBeforeExiting(bool editedImage, string aiProfile)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
            "shutdown-ai", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string image = Path.Combine(isolated, "capture.png");
        string temp = Path.Combine(isolated, "temp");
        string logPath = Path.Combine(temp, "capturetool.log");
        CreateOcrFixtureImage(image);
        using var app = LaunchApp(ResolveAppExecutablePath(repo), image,
            Path.Combine(isolated, "data"), temp, "en-US", detailsFixture: true, aiProfile: aiProfile);
        using var process = Process.GetProcessById(app.ProcessId);
        _ = process.Handle;
        using var automation = new UIA3Automation();
        try
        {
            var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
            WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
            if (editedImage)
                WaitForElement(window, automation, "ImageEdit_RotateButton", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
            WaitForElement(window, automation, "ImageEdit_TextExtractionButton", InteractionTimeout).Click();
            var consent = WaitForElement(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
            WaitForElementByName(consent, automation, "Allow local AI", InteractionTimeout).AsButton().Invoke();
            WaitForElementRemoved(window, automation, "CaptureMemoryConsentDialog", InteractionTimeout);
            string source = editedImage ? "editor" : "background";
            WaitFor(() => ReadLog().Contains($"UI test inference started: {source}.", StringComparison.Ordinal)
                ? window : null, InteractionTimeout, "AI provider running");

            ClickNativeCloseButton(window);
            if (editedImage)
            {
                // Canceling the close must preserve both the edit and running AI.
                WaitForElementByName(window, automation, "Cancel", InteractionTimeout).AsButton().Invoke();
                Assert.IsFalse(process.HasExited);
                Assert.IsFalse(ReadLog().Contains($"UI test inference canceled: {source}.", StringComparison.Ordinal));
                ClickNativeCloseButton(window);
                WaitForElementByName(window, automation, "Discard", InteractionTimeout).AsButton().Invoke();
            }

            Assert.IsTrue(process.WaitForExit((int)AppLaunchTimeout.TotalMilliseconds));
            Assert.AreEqual(0, process.ExitCode);
            string log = ReadLog();
            StringAssert.Contains(log, $"UI test inference canceled: {source}.");
            StringAssert.Contains(log, $"UI test inference drained: {source}.");
            StringAssert.Contains(log, "WinUI application loop exited normally.");
            Assert.IsLessThan(log.IndexOf("WinUI application loop exited normally.", StringComparison.Ordinal),
                log.IndexOf($"UI test inference drained: {source}.", StringComparison.Ordinal));
            Assert.IsFalse(log.Contains("ERROR:", StringComparison.Ordinal), log);
        }
        finally
        {
            if (File.Exists(logPath)) TestContext.AddResultFile(logPath);
        }

        string ReadLog() => File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
    }

    [TestMethod]
    [DataRow(false, false, 0)]
    [DataRow(true, false, 0)]
    [DataRow(false, true, 0)]
    [DataRow(true, true, 0)]
    [DataRow(false, false, 10000)]
    [DataRow(false, true, 10000)]
    [TestCategory("UI")]
    public void Shutdown_ReturnsFromWinUiMessageLoop(bool useExitMenu, bool zoomBeforeExit, int idleMilliseconds)
    {
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable isolated desktop UI tests.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        string isolated = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
            "shutdown", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        string image = Path.Combine(isolated, "capture.png");
        CreateOcrFixtureImage(image);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            string attemptDirectory = Path.Combine(isolated, attempt.ToString());
            string temp = Path.Combine(attemptDirectory, "temp");
            using var app = LaunchApp(ResolveAppExecutablePath(repo), image,
                Path.Combine(attemptDirectory, "data"), temp, "en-US", detailsFixture: true);
            using var process = Process.GetProcessById(app.ProcessId);
            // Keep a process handle open so the exit code remains available
            // after Windows removes the process from its active process list.
            _ = process.Handle;
            using var automation = new UIA3Automation();
            try
            {
                var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
                WaitForElement(window, automation, "ImageEdit_CommandBar", AppLaunchTimeout);
                var zoom = WaitForElement(window, automation, "ZoomSlider", AppLaunchTimeout);
                WaitFor(() => zoom.IsEnabled ? zoom : null, InteractionTimeout, "enabled zoom slider");
                if (idleMilliseconds > 0) Thread.Sleep(idleMilliseconds);
                if (zoomBeforeExit)
                {
                    zoom.Patterns.RangeValue.Pattern.SetValue(200);
                }
                if (useExitMenu)
                {
                    WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
                    WaitForElementByName(window, automation, "Exit", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
                }
                else
                {
                    window.CaptureToFile(Path.Combine(temp, "before-close.png"));
                    ClickNativeCloseButton(window);
                }

                Assert.IsTrue(process.WaitForExit((int)AppLaunchTimeout.TotalMilliseconds),
                    "Shutdown must finish without killing the app from the test harness.");
                Assert.AreEqual(0, process.ExitCode, "Normal close must not produce a native crash.");
                string log = File.ReadAllText(Path.Combine(temp, "capturetool.log"));
                // Environment.Exit(0) can return exit code zero while bypassing
                // WinUI shutdown. Require the event loop to actually unwind.
                StringAssert.Contains(log, "WinUI application loop exited normally.");
                Assert.IsFalse(log.Contains("ERROR:", StringComparison.Ordinal), log);
            }
            finally
            {
                string log = Path.Combine(temp, "capturetool.log");
                if (File.Exists(log)) TestContext.AddResultFile(log);
            }
        }
    }

    private static void ClickNativeCloseButton(Window window)
    {
        // WinUI's custom title bar exposes an invokable UIA close button without
        // a clickable point. Ask DWM for the real caption buttons' geometry.
        window.SetForeground();
        int result = DwmGetWindowAttribute(window.Properties.NativeWindowHandle.Value,
            5 /* DWMWA_CAPTION_BUTTON_BOUNDS */, out CaptionButtonBounds buttons, Marshal.SizeOf<CaptionButtonBounds>());
        Assert.AreEqual(0, result);
        Assert.IsGreaterThan(buttons.Top, buttons.Bottom);
        // Extended WinUI captions can report zero width. Their close button is
        // 46 logical pixels wide; DWM still supplies its right and vertical edges.
        int halfWidth = buttons.Right > buttons.Left ? (buttons.Right - buttons.Left) / 6
            : (int)(23 * GetDpiForWindow(window.Properties.NativeWindowHandle.Value) / 96);
        var bounds = window.BoundingRectangle;
        window.SetForeground();
        Mouse.Click(new Point(bounds.Left + buttons.Right - halfWidth,
            bounds.Top + (buttons.Top + buttons.Bottom) / 2));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CaptionButtonBounds
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute,
        out CaptionButtonBounds value, int size);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}

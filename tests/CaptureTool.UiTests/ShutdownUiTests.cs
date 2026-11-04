using FlaUI.UIA3;
using System.Diagnostics;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [TestCategory("UI")]
    public void Shutdown_ReturnsFromWinUiMessageLoop(bool useExitMenu, bool zoomBeforeExit)
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
                if (zoomBeforeExit)
                {
                    var zoom = WaitForElement(window, automation, "ZoomSlider", AppLaunchTimeout);
                    WaitFor(() => zoom.IsEnabled ? zoom : null, InteractionTimeout, "enabled zoom slider")
                        .Patterns.RangeValue.Pattern.SetValue(200);
                }
                if (useExitMenu)
                {
                    WaitForElementByName(window, automation, "File", InteractionTimeout).Click();
                    WaitForElementByName(window, automation, "Exit", InteractionTimeout).Patterns.Invoke.Pattern.Invoke();
                }
                else
                {
                    window.Close();
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
}

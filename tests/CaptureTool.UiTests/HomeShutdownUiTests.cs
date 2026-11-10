using FlaUI.UIA3;
using System.Diagnostics;

namespace CaptureTool.UiTests;

public sealed partial class ImageEditTextExtractionUiTests
{
    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(true, 150)]
    [DataRow(true, 1000)]
    [DataRow(false, 0)]
    [TestCategory("UI")]
    public void Shutdown_FromHomeImmediately_ReturnsNormally(bool isolated, int delayMilliseconds)
    {
        // Exercise app teardown without Visual Studio's injected XAML diagnostics.
        // Debugger-only native crashes need a separate F5 regression check.
        if (!ShouldRunUiTests()) Assert.Inconclusive("Enable desktop UI tests.");
        if (!isolated && Environment.GetEnvironmentVariable("CAPTURETOOL_UI_TEST_REAL_APP") != "1")
            Assert.Inconclusive("Opt in to testing the normal application launch.");
        using var dpi = new DesktopDpiScope();
        string repo = FindRepositoryRoot();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            string directory = Path.Combine(repo, "tests", "CaptureTool.UiTests", "TestResults", "artifacts",
                "home-shutdown", Guid.NewGuid().ToString("N"));
            string temp = Path.Combine(directory, "temp");
            Directory.CreateDirectory(temp);
            string executable = ResolveAppExecutablePath(repo);
            using var app = isolated
                ? LaunchApp(executable, string.Empty, Path.Combine(directory, "data"), temp, "en-US")
                : new LaunchedCaptureToolApp(UiTestAppLauncher.Launch(new ProcessStartInfo(executable)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(executable)
                }));
            using var process = Process.GetProcessById(app.ProcessId);
            _ = process.Handle;
            using var automation = new UIA3Automation();
            try
            {
                var window = WaitForMainWindow(app, automation, AppLaunchTimeout);
                WaitForElement(window, automation, "Home_NewVideoCaptureButton", AppLaunchTimeout);
                if (delayMilliseconds > 0) Thread.Sleep(delayMilliseconds);
                ClickNativeCloseButton(window);
                Assert.IsTrue(process.WaitForExit((int)AppLaunchTimeout.TotalMilliseconds), "Home shutdown timed out.");
                Assert.AreEqual(0, process.ExitCode, $"Home close crashed, PID {process.Id}, attempt {attempt}.");
                if (isolated)
                {
                    string log = File.ReadAllText(Path.Combine(temp, "capturetool.log"));
                    StringAssert.Contains(log, "WinUI application loop exited normally.");
                    Assert.IsFalse(log.Contains("ERROR:", StringComparison.Ordinal), log);
                }
            }
            finally
            {
                string logPath = Path.Combine(temp, "capturetool.log");
                if (File.Exists(logPath)) TestContext.AddResultFile(logPath);
            }
        }
    }
}

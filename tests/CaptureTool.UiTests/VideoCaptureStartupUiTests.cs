using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace CaptureTool.UiTests;

[TestClass]
public sealed class VideoCaptureStartupUiTests
{
    private const int GwlExStyle = -20;
    private const long ShadowWindowExtendedStyles =
        0x00000008L | // WS_EX_TOPMOST
        0x00000020L | // WS_EX_TRANSPARENT
        0x00000080L | // WS_EX_TOOLWINDOW
        0x00080000L | // WS_EX_LAYERED
        0x08000000L;  // WS_EX_NOACTIVATE
    private const uint WdaExcludeFromCapture = 0x00000011;

    private static readonly TimeSpan AppLaunchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InteractionTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RecordingStartTimeout = TimeSpan.FromSeconds(20);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("UI")]
    public void VideoCaptureFromHome_ShouldReachRecordingState()
    {
        if (!ShouldRunUiTests())
        {
            Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        }

        string repoRoot = FindRepositoryRoot();
        string appExecutablePath = ResolveAppExecutablePath(repoRoot);
        Assert.IsTrue(File.Exists(appExecutablePath), $"The WinUI app should be built at {appExecutablePath}.");

        string artifactDirectory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? TestContext.TestRunDirectory ?? Path.GetTempPath(),
            "CaptureTool.UiTests",
            Guid.NewGuid().ToString("N"));
        string appDataDirectory = Path.Combine(artifactDirectory, "app-data");
        string appTempDirectory = Path.Combine(artifactDirectory, "app-temp");
        Directory.CreateDirectory(appDataDirectory);
        Directory.CreateDirectory(appTempDirectory);

        using LaunchedCaptureToolApp app = LaunchApp(
            appExecutablePath,
            appDataDirectory,
            appTempDirectory);

        try
        {
            using var automation = new UIA3Automation();
            Window mainWindow = WaitForMainWindow(app, automation, AppLaunchTimeout);
            mainWindow.Focus();

            AutomationElement newVideoCaptureButton = WaitForElement(
                app,
                automation,
                "Home_NewVideoCaptureButton",
                AppLaunchTimeout);
            newVideoCaptureButton.Click();

            WaitForElement(
                app,
                automation,
                "CaptureModeSegmentedControl",
                InteractionTimeout);

            // Video capture defaults to full-screen. A click on the selection surface
            // confirms that target and opens the recording toolbar.
            ConfirmFullScreenSelection(app, automation);

            AutomationElement startRecordingButton = WaitForElement(
                app,
                automation,
                "StartVideoCaptureButton",
                InteractionTimeout);
            AutomationElement captureToolbar = startRecordingButton.Parent ??
                throw new InvalidOperationException("The recording button should have a toolbar parent.");
            AutomationElement closeOverlayButton = WaitForElement(
                app,
                automation,
                "CaptureOverlay_CloseButton",
                InteractionTimeout);

            Assert.IsTrue(
                captureToolbar.BoundingRectangle.Contains(startRecordingButton.BoundingRectangle),
                "The start button should fit inside the capture toolbar window.");
            Assert.IsTrue(
                captureToolbar.BoundingRectangle.Contains(closeOverlayButton.BoundingRectangle),
                "The rightmost toolbar button should not overflow the capture toolbar window.");

            nint shadowWindow = FindWindow("CaptureOverlayWindowShadow", null);
            Assert.AreNotEqual(nint.Zero, shadowWindow, "The toolbar shadow window should exist.");
            Assert.IsTrue(IsWindowVisible(shadowWindow), "The toolbar shadow window should be visible.");

            nint toolbarWindow = FindWindow("CaptureOverlayWindow", null);
            Assert.AreNotEqual(nint.Zero, toolbarWindow, "The capture toolbar window should exist.");
            Assert.IsTrue(
                GetWindowRect(toolbarWindow, out NativeRect toolbarBounds),
                "The capture toolbar bounds should be available.");
            Assert.IsTrue(
                GetWindowRect(shadowWindow, out NativeRect shadowBounds),
                "The toolbar shadow bounds should be available.");
            Assert.IsTrue(
                shadowBounds.Left < toolbarBounds.Left &&
                shadowBounds.Top < toolbarBounds.Top &&
                shadowBounds.Right > toolbarBounds.Right &&
                shadowBounds.Bottom > toolbarBounds.Bottom,
                "The shadow window should provide visible padding around every toolbar edge.");

            long shadowExtendedStyles = GetWindowLongPtr(shadowWindow, GwlExStyle);
            Assert.AreEqual(
                ShadowWindowExtendedStyles,
                shadowExtendedStyles & ShadowWindowExtendedStyles,
                "The shadow window should be layered, click-through, topmost, tool-only, and non-activating.");
            Assert.IsTrue(
                GetWindowDisplayAffinity(shadowWindow, out uint shadowDisplayAffinity) &&
                shadowDisplayAffinity == WdaExcludeFromCapture,
                "The toolbar shadow should be excluded from screen capture.");

            startRecordingButton.Click();

            AutomationElement stopRecordingButton = WaitForRecordingState(
                app,
                automation,
                RecordingStartTimeout);

            Assert.IsFalse(
                IsElementVisible(FindElement(app, automation, "RecordingErrorInfoBar")),
                "The recording error InfoBar should not be visible after recording starts.");

            // Keep the recorder active long enough to exercise more than the first frame,
            // then stop through the same UI path a user would use.
            Thread.Sleep(500);
            stopRecordingButton.Click();

            WaitForElementHiddenOrRemoved(
                app,
                automation,
                "StopVideoCaptureButton",
                InteractionTimeout);

            FileInfo recordedFile = WaitForFinalizedRecording(appTempDirectory, InteractionTimeout);
            Assert.IsGreaterThan(0L, recordedFile.Length, "The recorded video should not be empty.");
            TestContext.WriteLine($"Recorded output: {recordedFile.FullName} ({recordedFile.Length} bytes)");

            Assert.IsTrue(app.IsProcessRunning(), "CaptureTool should remain running after recording is stopped.");
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(nint windowHandle, out uint affinity);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static LaunchedCaptureToolApp LaunchApp(
        string appExecutablePath,
        string appDataDirectory,
        string appTempDirectory)
    {
        ProcessStartInfo startInfo = new(appExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(appExecutablePath)
        };

        foreach (string argument in new[]
        {
            "--capturetool-ui-test",
            "--ui-test-data-dir",
            appDataDirectory,
            "--ui-test-temp-dir",
            appTempDirectory
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to launch CaptureTool.");
        return new LaunchedCaptureToolApp(
            process,
            Path.Combine(appTempDirectory, "capturetool.log"));
    }

    private static Window WaitForMainWindow(
        LaunchedCaptureToolApp app,
        UIA3Automation automation,
        TimeSpan timeout)
    {
        return WaitFor(
            () =>
            {
                EnsureAppIsRunning(app);
                return GetTopLevelElements(app, automation)
                    .Select(element => element.AsWindow())
                    .FirstOrDefault(window =>
                        window.BoundingRectangle.Width > 0 &&
                        window.BoundingRectangle.Height > 0);
            },
            timeout,
            "CaptureTool main window");
    }

    private static AutomationElement WaitForElement(
        LaunchedCaptureToolApp app,
        UIA3Automation automation,
        string automationId,
        TimeSpan timeout)
    {
        try
        {
            return WaitFor(
                () =>
                {
                    EnsureAppIsRunning(app);
                    AutomationElement? element = FindElement(app, automation, automationId);
                    return IsElementVisible(element) ? element : null;
                },
                timeout,
                $"visible element with AutomationId '{automationId}'");
        }
        catch (AssertFailedException ex)
        {
            Assert.Fail($"{ex.Message}{Environment.NewLine}{DescribeAutomationTree(app, automation)}");
            throw new UnreachableException();
        }
    }

    private static AutomationElement WaitForRecordingState(
        LaunchedCaptureToolApp app,
        UIA3Automation automation,
        TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            EnsureAppIsRunning(app);

            AutomationElement? errorInfoBar = FindElement(
                app,
                automation,
                "RecordingErrorInfoBar");
            if (IsElementVisible(errorInfoBar))
            {
                Assert.Fail(
                    $"Recording failed before reaching the recording state. " +
                    $"InfoBar text: '{GetElementText(errorInfoBar!)}'.{Environment.NewLine}" +
                    app.GetLogContents() + Environment.NewLine +
                    DescribeAutomationTree(app, automation));
            }

            AutomationElement? stopButton = FindElement(
                app,
                automation,
                "StopVideoCaptureButton");
            if (IsElementVisible(stopButton))
            {
                return stopButton!;
            }

            Thread.Sleep(200);
        }

        Assert.Fail(
            $"Timed out waiting for video capture to reach the recording state.{Environment.NewLine}" +
            DescribeAutomationTree(app, automation));
        throw new UnreachableException();
    }

    private static void ConfirmFullScreenSelection(
        LaunchedCaptureToolApp app,
        UIA3Automation automation)
    {
        AutomationElement? selectionWindow = GetTopLevelElements(app, automation)
            .FirstOrDefault(element => element.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("CaptureModeSegmentedControl")) is not null);
        Assert.IsNotNull(selectionWindow, "The primary selection overlay window should be available.");

        Rectangle bounds = selectionWindow.BoundingRectangle;
        Assert.IsGreaterThan(0, bounds.Width, "The selection overlay width should be greater than zero.");
        Assert.IsGreaterThan(0, bounds.Height, "The selection overlay height should be greater than zero.");

        // Avoid the toolbar at the top-center of the monitor.
        Point confirmationPoint = new(
            bounds.Left + (bounds.Width / 2),
            bounds.Top + ((bounds.Height * 3) / 4));
        Mouse.Click(confirmationPoint);
    }

    private static void WaitForElementHiddenOrRemoved(
        LaunchedCaptureToolApp app,
        UIA3Automation automation,
        string automationId,
        TimeSpan timeout)
    {
        WaitFor(
            () =>
            {
                EnsureAppIsRunning(app);
                return IsElementVisible(FindElement(app, automation, automationId))
                    ? null
                    : new object();
            },
            timeout,
            $"element with AutomationId '{automationId}' to be hidden or removed");
    }

    private static FileInfo WaitForFinalizedRecording(string appTempDirectory, TimeSpan timeout)
    {
        return WaitFor(
            () =>
            {
                foreach (string filePath in Directory.EnumerateFiles(appTempDirectory, "*.mp4"))
                {
                    FileInfo file = new(filePath);
                    file.Refresh();
                    if (file.Length <= 0)
                    {
                        continue;
                    }

                    try
                    {
                        using FileStream stream = File.Open(
                            filePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.None);
                        return file;
                    }
                    catch (IOException)
                    {
                        // The sink writer is still finalizing this recording.
                    }
                }

                return null;
            },
            timeout,
            "a finalized, non-empty MP4 recording");
    }

    private static AutomationElement? FindElement(
        LaunchedCaptureToolApp app,
        UIA3Automation automation,
        string automationId)
    {
        foreach (AutomationElement topLevelElement in GetTopLevelElements(app, automation))
        {
            AutomationElement? element = topLevelElement.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId(automationId));
            if (element is not null)
            {
                return element;
            }
        }

        return null;
    }

    private static AutomationElement[] GetTopLevelElements(
        LaunchedCaptureToolApp app,
        UIA3Automation automation)
    {
        try
        {
            return automation
                .GetDesktop()
                .FindAllChildren(automation.ConditionFactory.ByProcessId(app.ProcessId));
        }
        catch
        {
            return [];
        }
    }

    private static bool IsElementVisible(AutomationElement? element)
    {
        if (element is null)
        {
            return false;
        }

        try
        {
            return !element.IsOffscreen &&
                element.BoundingRectangle.Width > 0 &&
                element.BoundingRectangle.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetElementText(AutomationElement element)
    {
        try
        {
            string descendantText = string.Join(
                " ",
                element.FindAllDescendants()
                    .Select(descendant => GetPropertyValue(() => descendant.Name))
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
            return string.IsNullOrWhiteSpace(descendantText)
                ? GetPropertyValue(() => element.Name)
                : descendantText;
        }
        catch
        {
            return GetPropertyValue(() => element.Name);
        }
    }

    private static void EnsureAppIsRunning(LaunchedCaptureToolApp app)
    {
        if (!app.IsProcessRunning())
        {
            Assert.Fail(
                $"CaptureTool exited before the requested UI state appeared.{Environment.NewLine}" +
                app.GetLogContents());
        }
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

    private static string DescribeAutomationTree(
        LaunchedCaptureToolApp app,
        UIA3Automation automation)
    {
        StringBuilder builder = new();
        builder.AppendLine("CaptureTool automation tree snapshot:");
        foreach (AutomationElement topLevelElement in GetTopLevelElements(app, automation))
        {
            AppendAutomationTree(builder, topLevelElement, 0, 4, 120);
        }

        return builder.ToString();
    }

    private static int AppendAutomationTree(
        StringBuilder builder,
        AutomationElement element,
        int depth,
        int maxDepth,
        int remaining)
    {
        if (remaining <= 0)
        {
            return 0;
        }

        builder
            .Append(' ', depth * 2)
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
            remaining = AppendAutomationTree(builder, child, depth + 1, maxDepth, remaining);
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

    private sealed class LaunchedCaptureToolApp : IDisposable
    {
        private readonly string _logFilePath;

        public LaunchedCaptureToolApp(Process process, string logFilePath)
        {
            ProcessId = process.Id;
            _logFilePath = logFilePath;
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

        public string GetLogContents()
        {
            try
            {
                return File.Exists(_logFilePath)
                    ? $"CaptureTool log:{Environment.NewLine}{File.ReadAllText(_logFilePath)}"
                    : "CaptureTool log was not created.";
            }
            catch (Exception ex)
            {
                return $"CaptureTool log could not be read: {ex.Message}";
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
        string runtimeIdentifier = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ||
            platform.Equals("ARM64", StringComparison.OrdinalIgnoreCase)
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

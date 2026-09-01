using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CaptureTool.UiTests;

[TestClass]
public sealed class CaptureMemoryHomeUiTests
{
    private static readonly TimeSpan AppLaunchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InteractionTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RealAnalysisTimeout = TimeSpan.FromMinutes(2);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("UI")]
    public void CaptureMemory_EnableSearchOpenAndRemove_ShouldCompleteFromHome()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("CAPTURETOOL_RUN_UI_TESTS"),
            "1",
            StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        }

        string repoRoot = FindRepositoryRoot();
        string executable = ResolveAppExecutablePath(repoRoot);
        Assert.IsTrue(File.Exists(executable), $"The WinUI app should be built at {executable}.");

        string artifacts = Path.Combine(
            TestContext.TestRunResultsDirectory ?? TestContext.TestRunDirectory ?? Path.GetTempPath(),
            "CaptureTool.UiTests",
            Guid.NewGuid().ToString("N"));
        string dataDirectory = Path.Combine(artifacts, "app-data");
        string temporaryDirectory = Path.Combine(artifacts, "app-temp");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(temporaryDirectory);
        string capturePath = Path.Combine(artifacts, "purple-comet.png");
        CreateSyntheticCapture(capturePath);

        using LaunchedApp app = Launch(
            executable,
            dataDirectory,
            temporaryDirectory,
            capturePath);
        try
        {
            using var automation = new UIA3Automation();
            Window window = WaitFor(
                () => GetTopLevelElements(app.ProcessId, automation)
                    .Select(element => element.AsWindow())
                    .FirstOrDefault(candidate => candidate.BoundingRectangle.Width > 0),
                AppLaunchTimeout,
                "Capture Tool main window");
            window.Focus();

            EnableWithExistingCaptures(app.ProcessId, automation, "Home");

            AutomationElement searchBox = WaitForElement(
                app.ProcessId,
                automation,
                "Home_CaptureMemorySearchBox");
            searchBox.Focus();
            Keyboard.Type("purple comet");

            AutomationElement openButton = WaitForElement(
                app.ProcessId,
                automation,
                "Home_CaptureMemoryOpenButton");
            AutomationElement results = WaitForElement(
                app.ProcessId,
                automation,
                "Home_CaptureMemoryResults");
            Assert.IsTrue(
                results.FindAllDescendants().Any(element =>
                    element.Name.Contains("Text match", StringComparison.OrdinalIgnoreCase) ||
                    element.Name.Contains("PURPLE COMET", StringComparison.OrdinalIgnoreCase)),
                "The result should explain that recognized text matched the query.");
            Assert.IsNotNull(
                FindElement(app.ProcessId, automation, "Home_CaptureMemoryDeleteButton"),
                "An app-owned retained source should expose the Delete capture action.");

            openButton.Click();
            WaitFor(
                () => File.Exists(Path.Combine(temporaryDirectory, "capture-memory-opened.marker"))
                    ? new object()
                    : null,
                InteractionTimeout,
                "the selected Memory result to open");

            WaitForElement(app.ProcessId, automation, "Home_CaptureMemoryRemoveButton").Click();
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Remove from Memory").Click();
            WaitFor(
                () => FindElement(app.ProcessId, automation, "Home_CaptureMemoryOpenButton") == null
                    ? new object()
                    : null,
                InteractionTimeout,
                "the removed Memory result to leave search results");
            Assert.IsTrue(
                File.Exists(Path.Combine(temporaryDirectory, "capture-memory-removed.marker")),
                "Removing a result should invoke the app-owned metadata cleanup boundary.");
        }
        finally
        {
            app.Close();
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    public void CaptureMemory_SettingsLifecycle_ShouldKeepIndexAndSourceWorkDistinct()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("CAPTURETOOL_RUN_UI_TESTS"),
            "1",
            StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        }

        string repoRoot = FindRepositoryRoot();
        string executable = ResolveAppExecutablePath(repoRoot);
        Assert.IsTrue(File.Exists(executable), $"The WinUI app should be built at {executable}.");

        string artifacts = Path.Combine(
            TestContext.TestRunResultsDirectory ?? TestContext.TestRunDirectory ?? Path.GetTempPath(),
            "CaptureTool.UiTests",
            Guid.NewGuid().ToString("N"));
        string dataDirectory = Path.Combine(artifacts, "app-data");
        string temporaryDirectory = Path.Combine(artifacts, "app-temp");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(temporaryDirectory);
        string capturePath = Path.Combine(artifacts, "purple-comet.png");
        CreateSyntheticCapture(capturePath);

        using LaunchedApp app = Launch(
            executable,
            dataDirectory,
            temporaryDirectory,
            capturePath);
        try
        {
            using var automation = new UIA3Automation();
            Window window = WaitFor(
                () => GetTopLevelElements(app.ProcessId, automation)
                    .Select(element => element.AsWindow())
                    .FirstOrDefault(candidate => candidate.BoundingRectangle.Width > 0),
                AppLaunchTimeout,
                "Capture Tool main window");
            window.Focus();

            WaitForElement(app.ProcessId, automation, "AppMenu_FileMenuItem").Click();
            Invoke(WaitForElement(app.ProcessId, automation, "AppMenu_SettingsItem"));
            EnableWithExistingCaptures(app.ProcessId, automation, "Settings");
            WaitForMarker(temporaryDirectory, "capture-memory-backfilled.marker");

            AutomationElement clear = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryClearButton");
            FocusAndClick(clear);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Delete analyzed data").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-cleared.marker");

            Expand(WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryExpander"));

            AutomationElement reanalyze = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryReanalyzeButton");
            FocusAndClick(reanalyze);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Reanalyze captures").Click();
            AutomationElement activityButton = WaitForElement(
                app.ProcessId,
                automation,
                "MainWindow_BackgroundActivityButton");
            Assert.IsGreaterThanOrEqualTo(
                280,
                activityButton.BoundingRectangle.Width,
                "The activity button should stretch across its compact card instead of shrinking around its text.");
            Assert.IsNull(
                FindElement(app.ProcessId, automation, "Settings_CaptureMemoryOperationStatus"),
                "Capture Memory activity feedback should only appear in the floating activity UI.");
            WaitForMarker(temporaryDirectory, "capture-memory-reanalyzed.marker");

            AutomationElement rebuild = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryRebuildButton");
            FocusAndClick(rebuild);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Rebuild index").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-rebuilt.marker");

            AutomationElement analysisToggle = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryAnalyzeNewToggle");
            Toggle(analysisToggle);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Stop analyzing").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-stopped.marker");

            Toggle(analysisToggle);
            WaitForMarker(temporaryDirectory, "capture-memory-resumed.marker");

            AutomationElement turnOff = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryTurnOffButton");
            FocusAndClick(turnOff);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Turn off and erase").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-erased.marker");

            WaitFor(
                () => GetToggleState(analysisToggle) == FlaUI.Core.Definitions.ToggleState.Off
                    ? new object()
                    : null,
                InteractionTimeout,
                "Capture Memory toggle to turn off");

            Toggle(analysisToggle);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryEnableDialog");
            WaitForElementByName(app.ProcessId, automation, "New captures only").Click();
            WaitFor(
                () => GetToggleState(analysisToggle) == FlaUI.Core.Definitions.ToggleState.On
                    ? new object()
                    : null,
                InteractionTimeout,
                "Capture Memory to turn back on from Settings");
        }
        finally
        {
            app.Close();
        }
    }

    [TestMethod]
    [TestCategory("UI")]
    public void CaptureMemory_RealAnalysisBackfill_ShouldFindExistingCaptureTextFragments()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("CAPTURETOOL_RUN_UI_TESTS"),
            "1",
            StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive("Set CAPTURETOOL_RUN_UI_TESTS=1 to run desktop UI automation tests.");
        }

        string repoRoot = FindRepositoryRoot();
        string executable = ResolveAppExecutablePath(repoRoot);
        Assert.IsTrue(File.Exists(executable), $"The WinUI app should be built at {executable}.");

        string artifacts = Path.Combine(
            TestContext.TestRunResultsDirectory ?? TestContext.TestRunDirectory ?? Path.GetTempPath(),
            "CaptureTool.RealAnalysisUiTests",
            Guid.NewGuid().ToString("N"));
        string dataDirectory = Path.Combine(artifacts, "app-data");
        string temporaryDirectory = Path.Combine(artifacts, "app-temp");
        string retainedCaptureDirectory = Path.Combine(dataDirectory, "Captures");
        Directory.CreateDirectory(retainedCaptureDirectory);
        Directory.CreateDirectory(temporaryDirectory);
        CreateSyntheticCapture(Path.Combine(retainedCaptureDirectory, "retained-a.png"));

        using LaunchedApp app = LaunchRealCaptureAnalysis(
            executable,
            dataDirectory,
            temporaryDirectory);
        try
        {
            using var automation = new UIA3Automation();
            Window window = WaitFor(
                () => GetTopLevelElements(app.ProcessId, automation)
                    .Select(element => element.AsWindow())
                    .FirstOrDefault(candidate => candidate.BoundingRectangle.Width > 0),
                AppLaunchTimeout,
                "Capture Tool main window");
            window.Focus();

            EnableWithExistingCaptures(app.ProcessId, automation, "Home");
            AutomationElement searchBox = WaitFor(
                () => FindElement(app.ProcessId, automation, "Home_CaptureMemorySearchBox"),
                RealAnalysisTimeout,
                "Capture Memory search after real model preparation");
            AutomationElement openButton = WaitForRealAnalysisResult(
                app.ProcessId,
                automation,
                searchBox,
                "urple come");
            AutomationElement results = WaitForElement(
                app.ProcessId,
                automation,
                "Home_CaptureMemoryResults");

            Assert.IsTrue(
                results.FindAllDescendants().Any(element =>
                    TryGetElementName(element).Contains("Text match", StringComparison.OrdinalIgnoreCase)),
                "The real Windows OCR result should explain the recognized-text match.");
            Assert.IsTrue(openButton.IsEnabled, "The analyzed existing capture should be openable.");
            Assert.IsGreaterThanOrEqualTo(
                2,
                Directory.EnumerateFiles(
                        Path.Combine(temporaryDirectory, "LocalCache", "CaptureAnalysis", "jobs-v1"),
                        "*.job",
                        SearchOption.AllDirectories)
                    .Count(),
                "The real pipeline should persist jobs for the required image capabilities.");
            Assert.IsTrue(
                Directory.EnumerateFiles(
                    Path.Combine(temporaryDirectory, "LocalCache", "CaptureAnalysis", "metadata-v1"),
                    "*.analysis",
                    SearchOption.AllDirectories).Any(),
                "The real pipeline should persist a protected metadata envelope.");

            // This is the release-critical lifecycle seam: clearing removes canonical metadata
            // and the projection, then reanalysis must recreate both through the durable worker.
            WaitForElement(app.ProcessId, automation, "AppMenu_FileMenuItem").Click();
            Invoke(WaitForElement(app.ProcessId, automation, "AppMenu_SettingsItem"));
            FocusAndClick(WaitForElement(app.ProcessId, automation, "Settings_CaptureMemoryClearButton"));
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Delete analyzed data").Click();
            AutomationElement expander = WaitForElement(
                app.ProcessId, automation, "Settings_CaptureMemoryExpander");
            Expand(expander);
            AutomationElement reanalyze = WaitForElement(
                app.ProcessId, automation, "Settings_CaptureMemoryReanalyzeButton");
            WaitFor(() => reanalyze.IsEnabled ? new object() : null,
                InteractionTimeout, "Reanalyze to become available after clearing Memory");
            FocusAndClick(reanalyze);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Reanalyze captures").Click();
            WaitFor(() => !reanalyze.IsEnabled ? new object() : null,
                InteractionTimeout, "reanalysis scheduling to start");
            WaitFor(() => reanalyze.IsEnabled ? new object() : null,
                RealAnalysisTimeout, "reanalysis scheduling to finish");

            WaitForElement(app.ProcessId, automation, "AppMenu_FileMenuItem").Click();
            Invoke(WaitForElement(app.ProcessId, automation, "AppMenu_HomeItem"));
            AutomationElement restoredSearch = WaitForElement(
                app.ProcessId, automation, "Home_CaptureMemorySearchBox");
            _ = WaitForRealAnalysisResult(
                app.ProcessId, automation, restoredSearch, "urple come");
            Assert.IsTrue(
                Directory.EnumerateFiles(
                    Path.Combine(temporaryDirectory, "LocalCache", "CaptureAnalysis", "metadata-v1"),
                    "*.analysis",
                    SearchOption.AllDirectories).Any(),
                "Reanalysis should recreate protected canonical metadata before search returns.");
        }
        finally
        {
            app.Close();
        }
    }

    private static void EnableWithExistingCaptures(int processId, UIA3Automation automation, string page)
    {
        if (page == "Settings")
        {
            Expand(WaitForElement(processId, automation, "Settings_CaptureMemoryExpander"));
            Toggle(WaitForElement(processId, automation, "Settings_CaptureMemoryAnalyzeNewToggle"));
        }
        else
        {
            FocusAndClick(WaitForElement(processId, automation, "Home_CaptureMemoryEnableButton"));
        }

        WaitForElement(processId, automation, "CaptureMemoryEnableDialog");
        WaitForElementByName(processId, automation, "Scan existing captures").Click();
    }

    private static void FocusAndClick(AutomationElement element)
    {
        element.AsButton().Invoke();
    }

    private static void Invoke(AutomationElement element)
    {
        Assert.IsTrue(element.Patterns.Invoke.IsSupported);
        element.Patterns.Invoke.Pattern.Invoke();
    }

    private static void Expand(AutomationElement element)
    {
        Assert.IsTrue(
            element.Patterns.ExpandCollapse.IsSupported,
            "The Settings troubleshooting control should expose the expand/collapse pattern.");
        element.Patterns.ExpandCollapse.Pattern.Expand();
    }

    private static void Toggle(AutomationElement element)
    {
        Assert.IsTrue(element.Patterns.Toggle.IsSupported);
        element.Patterns.Toggle.Pattern.Toggle();
    }

    private static FlaUI.Core.Definitions.ToggleState GetToggleState(
        AutomationElement element)
    {
        Assert.IsTrue(element.Patterns.Toggle.IsSupported);
        return element.Patterns.Toggle.Pattern.ToggleState.Value;
    }

    private static void WaitForMarker(string directory, string filename)
    {
        WaitFor(
            () => File.Exists(Path.Combine(directory, filename)) ? new object() : null,
            InteractionTimeout,
            $"marker '{filename}'");
    }

    private static void CreateSyntheticCapture(string path)
    {
        using var bitmap = new Bitmap(800, 600);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.MidnightBlue);
        using var titleFont = new Font("Segoe UI", 48, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 24);
        using var titleBrush = new SolidBrush(Color.MediumPurple);
        graphics.DrawString("PURPLE COMET", titleFont, titleBrush, new PointF(40, 60));
        graphics.DrawString("Project launch checklist", bodyFont, Brushes.White, new PointF(44, 150));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static LaunchedApp Launch(
        string executable,
        string dataDirectory,
        string temporaryDirectory,
        string capturePath)
    {
        string[] arguments =
        {
            "--capturetool-ui-test",
            "--ui-test-capture-memory",
            "--ui-test-capture-memory-image",
            capturePath,
            "--ui-test-data-dir",
            dataDirectory,
            "--ui-test-temp-dir",
            temporaryDirectory,
        };
        return new LaunchedApp(LaunchProcess(executable, arguments));
    }

    private static LaunchedApp LaunchRealCaptureAnalysis(
        string executable,
        string dataDirectory,
        string temporaryDirectory)
    {
        string[] arguments =
        {
            "--capturetool-ui-test",
            "--ui-test-enable-capture-analysis",
            "--ui-test-data-dir",
            dataDirectory,
            "--ui-test-temp-dir",
            temporaryDirectory,
        };
        return new LaunchedApp(LaunchProcess(executable, arguments));
    }

    private static Process LaunchProcess(string executable, IReadOnlyList<string> arguments)
    {
        string? appUserModelId = Environment.GetEnvironmentVariable(
            "CAPTURETOOL_UI_TEST_APP_ID");
        if (!string.IsNullOrWhiteSpace(appUserModelId))
        {
            var activationManager = (IApplicationActivationManager)new ApplicationActivationManager();
            int result = activationManager.ActivateApplication(
                appUserModelId,
                string.Join(' ', arguments.Select(QuoteArgument)),
                ActivateOptions.NoErrorUI,
                out uint processId);
            Marshal.ThrowExceptionForHR(result);
            return Process.GetProcessById(checked((int)processId));
        }

        ProcessStartInfo startInfo = new(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable),
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to launch Capture Tool.");
    }

    private static string QuoteArgument(string value) =>
        $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static AutomationElement WaitForRealAnalysisResult(
        int processId,
        UIA3Automation automation,
        AutomationElement searchBox,
        string query)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        TextBox textBox = searchBox.AsTextBox();
        while (stopwatch.Elapsed < RealAnalysisTimeout)
        {
            textBox.Text = string.Empty;
            textBox.Text = query;
            Thread.Sleep(750);
            AutomationElement? result = FindElement(
                processId,
                automation,
                "Home_CaptureMemoryOpenButton");
            if (result != null)
            {
                return result;
            }
        }

        Assert.Fail("Timed out waiting for a real Capture Memory analysis result.");
        throw new UnreachableException();
    }

    private static AutomationElement WaitForElement(
        int processId,
        UIA3Automation automation,
        string automationId)
    {
        return WaitFor(
            () => FindElement(processId, automation, automationId),
            AppLaunchTimeout,
            $"element '{automationId}'");
    }

    private static AutomationElement? FindElement(
        int processId,
        UIA3Automation automation,
        string automationId)
    {
        foreach (AutomationElement topLevel in GetTopLevelElements(processId, automation))
        {
            AutomationElement? result = topLevel.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId(automationId));
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static string TryGetElementName(AutomationElement element)
    {
        try
        {
            return element.Name;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static AutomationElement WaitForElementByName(
        int processId,
        UIA3Automation automation,
        string name)
    {
        return WaitFor(
            () =>
            {
                foreach (AutomationElement topLevel in GetTopLevelElements(processId, automation))
                {
                    AutomationElement? result = topLevel.FindFirstDescendant(
                        automation.ConditionFactory.ByName(name));
                    if (result != null)
                    {
                        return result;
                    }
                }

                return null;
            },
            InteractionTimeout,
            $"element named '{name}'");
    }

    private static AutomationElement[] GetTopLevelElements(int processId, UIA3Automation automation)
    {
        try
        {
            return automation.GetDesktop().FindAllChildren(
                automation.ConditionFactory.ByProcessId(processId));
        }
        catch
        {
            return [];
        }
    }

    private static T WaitFor<T>(Func<T?> getValue, TimeSpan timeout, string description)
        where T : class
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            T? value = getValue();
            if (value != null)
            {
                return value;
            }

            Thread.Sleep(150);
        }

        Assert.Fail($"Timed out waiting for {description}.");
        throw new UnreachableException();
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
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CaptureTool.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private sealed class LaunchedApp : IDisposable
    {
        public LaunchedApp(Process process)
        {
            ProcessId = process.Id;
            process.Dispose();
        }

        public int ProcessId { get; }

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

        public void Dispose() => Close();
    }

    [Flags]
    private enum ActivateOptions : uint
    {
        NoErrorUI = 2,
    }

    [ComImport]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments,
            ActivateOptions options,
            out uint processId);

        [PreserveSig]
        int ActivateForFile(IntPtr appUserModelId, IntPtr itemArray, IntPtr verb, out uint processId);

        [PreserveSig]
        int ActivateForProtocol(IntPtr appUserModelId, IntPtr itemArray, out uint processId);
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager
    {
    }
}

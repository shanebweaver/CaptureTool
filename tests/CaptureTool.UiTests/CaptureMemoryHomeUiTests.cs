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

            WaitForElement(app.ProcessId, automation, "Home_CaptureMemoryEnableExistingButton").Click();

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

            WaitForElement(app.ProcessId, automation, "Home_CaptureMemoryEnableExistingButton").Click();
            WaitForElement(app.ProcessId, automation, "Home_CaptureMemorySearchBox");

            WaitForElement(app.ProcessId, automation, "AppMenu_FileMenuItem").Click();
            WaitForElement(app.ProcessId, automation, "AppMenu_SettingsItem").Click();

            AutomationElement clear = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryClearButton");
            FocusAndClick(clear);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Clear Memory").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-cleared.marker");

            AutomationElement reanalyze = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryReanalyzeButton");
            FocusAndClick(reanalyze);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Reanalyze captures").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-reanalyzed.marker");
            WaitForElement(app.ProcessId, automation, "Settings_CaptureMemoryOperationStatus");

            AutomationElement rebuild = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryRebuildButton");
            FocusAndClick(rebuild);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Rebuild index").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-rebuilt.marker");

            AutomationElement stop = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryStopButton");
            FocusAndClick(stop);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Stop analyzing").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-stopped.marker");

            AutomationElement resume = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryResumeButton");
            FocusAndClick(resume);
            WaitForMarker(temporaryDirectory, "capture-memory-resumed.marker");

            AutomationElement turnOff = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryTurnOffButton");
            FocusAndClick(turnOff);
            WaitForElement(app.ProcessId, automation, "CaptureMemoryConfirmationDialog");
            WaitForElementByName(app.ProcessId, automation, "Turn off and erase").Click();
            WaitForMarker(temporaryDirectory, "capture-memory-erased.marker");

            AutomationElement policyStatus = WaitForElement(
                app.ProcessId,
                automation,
                "Settings_CaptureMemoryPolicyStatus");
            WaitFor(
                () => policyStatus.Name.Contains("off", StringComparison.OrdinalIgnoreCase)
                    ? new object()
                    : null,
                InteractionTimeout,
                "Capture Memory to report that analysis is off");
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

            WaitForElement(app.ProcessId, automation, "Home_CaptureMemoryEnableExistingButton").Click();
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
        }
        finally
        {
            app.Close();
        }
    }

    private static void FocusAndClick(AutomationElement element)
    {
        element.AsButton().Invoke();
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
        ProcessStartInfo startInfo = new(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable),
        };
        foreach (string argument in new[]
        {
            "--capturetool-ui-test",
            "--ui-test-capture-memory",
            "--ui-test-capture-memory-image",
            capturePath,
            "--ui-test-data-dir",
            dataDirectory,
            "--ui-test-temp-dir",
            temporaryDirectory,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to launch Capture Tool.");
        return new LaunchedApp(process);
    }

    private static LaunchedApp LaunchRealCaptureAnalysis(
        string executable,
        string dataDirectory,
        string temporaryDirectory)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable),
        };
        foreach (string argument in new[]
        {
            "--capturetool-ui-test",
            "--ui-test-enable-capture-analysis",
            "--ui-test-data-dir",
            dataDirectory,
            "--ui-test-temp-dir",
            temporaryDirectory,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to launch Capture Tool.");
        return new LaunchedApp(process);
    }

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
}

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestLaunchOptions
{
    public const string DefaultInstanceKey = "MySingleInstanceApp";

    private UiTestLaunchOptions(
        bool isEnabled,
        string? imageFilePath,
        bool isCaptureMemoryEnabled,
        string? captureMemoryImageFilePath,
        string? dataFolderPath,
        string? temporaryFolderPath)
    {
        IsEnabled = isEnabled;
        ImageFilePath = imageFilePath;
        IsCaptureMemoryEnabled = isCaptureMemoryEnabled;
        CaptureMemoryImageFilePath = captureMemoryImageFilePath;
        DataFolderPath = dataFolderPath;
        TemporaryFolderPath = temporaryFolderPath;
    }

    public static UiTestLaunchOptions Current { get; private set; } = new(
        false,
        null,
        false,
        null,
        null,
        null);

    public bool IsEnabled { get; }

    public string? ImageFilePath { get; }

    public bool IsCaptureMemoryEnabled { get; }

    public string? CaptureMemoryImageFilePath { get; }

    public string? DataFolderPath { get; }

    public string? TemporaryFolderPath { get; }

    public static void Initialize(string[] args)
    {
        bool isEnabled = args.Contains("--capturetool-ui-test", StringComparer.OrdinalIgnoreCase);

        Current = new(
            isEnabled,
            GetOptionValue(args, "--ui-test-image"),
            args.Contains("--ui-test-capture-memory", StringComparer.OrdinalIgnoreCase),
            GetOptionValue(args, "--ui-test-capture-memory-image"),
            GetOptionValue(args, "--ui-test-data-dir"),
            GetOptionValue(args, "--ui-test-temp-dir"));
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}

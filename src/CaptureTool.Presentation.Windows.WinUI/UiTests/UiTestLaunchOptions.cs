namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestLaunchOptions
{
    public const string DefaultInstanceKey = "MySingleInstanceApp";

    private UiTestLaunchOptions(
        bool isEnabled,
        string instanceKey,
        string? imageFilePath,
        string? dataFolderPath,
        string? temporaryFolderPath)
    {
        IsEnabled = isEnabled;
        InstanceKey = instanceKey;
        ImageFilePath = imageFilePath;
        DataFolderPath = dataFolderPath;
        TemporaryFolderPath = temporaryFolderPath;
    }

    public static UiTestLaunchOptions Current { get; private set; } = new(
        false,
        DefaultInstanceKey,
        null,
        null,
        null);

    public bool IsEnabled { get; }

    public string InstanceKey { get; }

    public string? ImageFilePath { get; }

    public string? DataFolderPath { get; }

    public string? TemporaryFolderPath { get; }

    public static void Initialize(string[] args)
    {
        bool isEnabled = args.Contains("--capturetool-ui-test", StringComparer.OrdinalIgnoreCase);
        string instanceKey = GetOptionValue(args, "--ui-test-instance-key") ??
            (isEnabled ? $"CaptureToolUiTest-{Guid.NewGuid()}" : DefaultInstanceKey);

        Current = new(
            isEnabled,
            instanceKey,
            GetOptionValue(args, "--ui-test-image"),
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

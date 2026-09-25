namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestLaunchOptions
{
    public const string DefaultInstanceKey = "MySingleInstanceApp";

    private UiTestLaunchOptions(
        bool isEnabled,
        string? imageFilePath,
        string? dataFolderPath,
        string? temporaryFolderPath,
        string? language = null)
    {
        IsEnabled = isEnabled;
        ImageFilePath = imageFilePath;
        DataFolderPath = dataFolderPath;
        TemporaryFolderPath = temporaryFolderPath;
        Language = language;
    }

    public static UiTestLaunchOptions Current { get; private set; } = new(
        false,
        null,
        null,
        null);

    public bool IsEnabled { get; }

    public string? ImageFilePath { get; }

    public string? DataFolderPath { get; }

    public string? TemporaryFolderPath { get; }

    public string? Language { get; }

    public static bool DetailsFixture { get; private set; }
    public static bool CaptureFixture { get; private set; }

    public static void Initialize(string[] args)
    {
        bool isEnabled = args.Contains("--capturetool-ui-test", StringComparer.OrdinalIgnoreCase);
        DetailsFixture = isEnabled && args.Contains("--ui-test-details", StringComparer.OrdinalIgnoreCase);
        CaptureFixture = DetailsFixture && args.Contains("--ui-test-capture", StringComparer.OrdinalIgnoreCase);

        Current = new(
            isEnabled,
            GetOptionValue(args, "--ui-test-image"),
            GetOptionValue(args, "--ui-test-data-dir"),
            GetOptionValue(args, "--ui-test-temp-dir"),
            GetOptionValue(args, "--ui-test-language"));
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

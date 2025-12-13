namespace CaptureTool.Core.Interfaces.Actions.Settings;

public interface ISettingsActions
{
    bool CanGoBack();
    void GoBack();
    void RestartApp();

    // Image capture settings
    Task UpdateImageAutoCopyAsync(bool value, CancellationToken ct);
    Task UpdateImageAutoSaveAsync(bool value, CancellationToken ct);

    // Language and theme
    Task UpdateAppLanguageAsync(int index, CancellationToken ct);
    void UpdateAppTheme(int index);

    // Screenshots folder
    Task ChangeScreenshotsFolderAsync(CancellationToken ct);
    // Note: OpenScreenshotsFolder requires context (folder path) - use factory pattern in ViewModel

    // Temp files
    // Note: OpenTemporaryFilesFolder requires context (folder path) - use factory pattern in ViewModel
    void ClearTemporaryFiles(string tempFolderPath);

    // Defaults
    Task RestoreDefaultSettingsAsync(CancellationToken ct);
}

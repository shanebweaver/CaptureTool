using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces;

namespace CaptureTool.Core.Implementations.Actions.Settings;

public sealed partial class SettingsActions : ISettingsActions
{
    private readonly ISettingsGoBackAction _goBack;
    private readonly ISettingsRestartAppAction _restart;
    private readonly ISettingsUpdateImageAutoCopyAction _updateImageAutoCopy;
    private readonly ISettingsUpdateImageAutoSaveAction _updateImageAutoSave;
    private readonly ISettingsUpdateAppLanguageAction _updateLanguage;
    private readonly ISettingsUpdateAppThemeAction _updateTheme;
    private readonly ISettingsChangeScreenshotsFolderAction _changeScreenshotsFolder;
    private readonly ISettingsClearTempFilesAction _clearTempFiles;
    private readonly ISettingsRestoreDefaultsAction _restoreDefaults;

    public SettingsActions(
        ISettingsGoBackAction goBack,
        ISettingsRestartAppAction restart,
        ISettingsUpdateImageAutoCopyAction updateImageAutoCopy,
        ISettingsUpdateImageAutoSaveAction updateImageAutoSave,
        ISettingsUpdateAppLanguageAction updateLanguage,
        ISettingsUpdateAppThemeAction updateTheme,
        ISettingsChangeScreenshotsFolderAction changeScreenshotsFolder,
        ISettingsClearTempFilesAction clearTempFiles,
        ISettingsRestoreDefaultsAction restoreDefaults)
    {
        _goBack = goBack;
        _restart = restart;
        _updateImageAutoCopy = updateImageAutoCopy;
        _updateImageAutoSave = updateImageAutoSave;
        _updateLanguage = updateLanguage;
        _updateTheme = updateTheme;
        _changeScreenshotsFolder = changeScreenshotsFolder;
        _clearTempFiles = clearTempFiles;
        _restoreDefaults = restoreDefaults;
    }

    public bool CanGoBack() => _goBack.CanExecute();
    public void GoBack() => _goBack.ExecuteCommand();
    public void RestartApp() => _restart.ExecuteCommand();

    public Task UpdateImageAutoCopyAsync(bool value, CancellationToken ct) => _updateImageAutoCopy.ExecuteCommandAsync(value);
    public Task UpdateImageAutoSaveAsync(bool value, CancellationToken ct) => _updateImageAutoSave.ExecuteCommandAsync(value);

    public Task UpdateAppLanguageAsync(int index, CancellationToken ct) => _updateLanguage.ExecuteCommandAsync(index);
    public void UpdateAppTheme(int index) => _updateTheme.ExecuteCommand(index);

    public Task ChangeScreenshotsFolderAsync(CancellationToken ct) => _changeScreenshotsFolder.ExecuteCommandAsync();

    public void ClearTemporaryFiles(string tempFolderPath) => _clearTempFiles.ExecuteCommand(tempFolderPath);

    public Task RestoreDefaultSettingsAsync(CancellationToken ct) => _restoreDefaults.ExecuteCommandAsync();
}

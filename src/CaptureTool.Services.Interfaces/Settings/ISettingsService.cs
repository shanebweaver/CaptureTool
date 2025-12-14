using CaptureTool.Common.Settings;

namespace CaptureTool.Services.Interfaces.Settings;

/// <summary>
/// Provides services for managing application settings with persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Occurs when one or more settings have changed.
    /// </summary>
    event Action<ISettingDefinition[]>? SettingsChanged;

    /// <summary>
    /// Gets the value for a setting with a default value.
    /// </summary>
    /// <typeparam name="T">The type of the setting value.</typeparam>
    /// <param name="settingDefinition">The setting definition containing the key and default value.</param>
    /// <returns>The current value of the setting, or the default value if not set.</returns>
    T Get<T>(ISettingDefinitionWithValue<T> settingDefinition);

    /// <summary>
    /// Determines whether a setting has been explicitly set.
    /// </summary>
    /// <param name="settingDefinition">The setting definition to check.</param>
    /// <returns>True if the setting has been set; otherwise, false.</returns>
    bool IsSet(ISettingDefinition settingDefinition);

    /// <summary>
    /// Sets a boolean setting value.
    /// </summary>
    /// <param name="settingDefinition">The setting definition.</param>
    /// <param name="value">The value to set.</param>
    void Set(IBoolSettingDefinition settingDefinition, bool value);

    /// <summary>
    /// Sets a double setting value.
    /// </summary>
    /// <param name="settingDefinition">The setting definition.</param>
    /// <param name="value">The value to set.</param>
    void Set(IDoubleSettingDefinition settingDefinition, double value);

    /// <summary>
    /// Sets an integer setting value.
    /// </summary>
    /// <param name="settingDefinition">The setting definition.</param>
    /// <param name="value">The value to set.</param>
    void Set(IIntSettingDefinition settingDefinition, int value);

    /// <summary>
    /// Sets a string setting value.
    /// </summary>
    /// <param name="settingDefinition">The setting definition.</param>
    /// <param name="value">The value to set.</param>
    void Set(IStringSettingDefinition settingDefinition, string value);

    /// <summary>
    /// Removes a setting, reverting it to its default value.
    /// </summary>
    /// <param name="settingDefinition">The setting definition to unset.</param>
    void Unset(ISettingDefinition settingDefinition);

    /// <summary>
    /// Removes multiple settings, reverting them to their default values.
    /// </summary>
    /// <param name="settingDefinitions">The setting definitions to unset.</param>
    void Unset(ISettingDefinition[] settingDefinitions);

    /// <summary>
    /// Initializes the settings service with the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the settings file.</param>
    /// <param name="cancellationToken">A token to cancel the initialization.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to save all settings to the file.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the save operation.</param>
    /// <returns>A task representing the asynchronous operation, with a result indicating success.</returns>
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears all settings from memory (does not affect persisted file).
    /// </summary>
    void ClearAllSettings();
}

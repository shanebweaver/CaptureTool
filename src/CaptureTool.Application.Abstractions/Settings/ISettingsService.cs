namespace CaptureTool.Application.Abstractions.Settings;

public interface ISettingsService
{
    event Action<ISettingDefinition[]>? SettingsChanged;

    T Get<T>(ISettingDefinitionWithValue<T> settingDefinition);
    bool IsSet(ISettingDefinition settingDefinition);
    void Set(IBoolSettingDefinition settingDefinition, bool value);
    void Set(IDoubleSettingDefinition settingDefinition, double value);
    void Set(IIntSettingDefinition settingDefinition, int value);
    void Set(IStringSettingDefinition settingDefinition, string value);
    void Unset(ISettingDefinition settingDefinition);
    void Unset(ISettingDefinition[] settingDefinitions);

    Task<SettingsMutationResult> TrySetAndSaveAsync(
        IBoolSettingDefinition settingDefinition,
        bool value,
        CancellationToken cancellationToken);
    Task<SettingsMutationResult> TrySetAndSaveAsync(
        IDoubleSettingDefinition settingDefinition,
        double value,
        CancellationToken cancellationToken);
    Task<SettingsMutationResult> TrySetAndSaveAsync(
        IIntSettingDefinition settingDefinition,
        int value,
        CancellationToken cancellationToken);
    Task<SettingsMutationResult> TrySetAndSaveAsync(
        IStringSettingDefinition settingDefinition,
        string value,
        CancellationToken cancellationToken);
    Task<SettingsMutationResult> TryUnsetAndSaveAsync(
        ISettingDefinition settingDefinition,
        CancellationToken cancellationToken);
    Task<SettingsMutationResult> TryClearAllAndSaveAsync(CancellationToken cancellationToken);

    Task InitializeAsync(string filePath, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);

    void ClearAllSettings();
}

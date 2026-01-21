using CaptureTool.Common.Settings;

namespace CaptureTool.Infrastructure.Interfaces.Settings;

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

    Task InitializeAsync(string filePath, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);

    void ClearAllSettings();
}

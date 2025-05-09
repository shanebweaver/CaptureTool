namespace CaptureTool.Services.Settings;

using System;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Settings.Definitions;

public interface ISettingsService
{
    event Action<SettingDefinition[]>? SettingsChanged;

    T Get<T>(SettingDefinition<T> settingDefinition);
    bool IsSet(SettingDefinition settingDefinition);
    void Set(BoolSettingDefinition settingDefinition, bool value);
    void Set(DoubleSettingDefinition settingDefinition, double value);
    void Set(IntSettingDefinition settingDefinition, int value);
    void Set(StringSettingDefinition settingDefinition, string value);
    void Unset(SettingDefinition settingDefinition);
    void Unset(SettingDefinition[] settingDefinitions);

    Task InitializeAsync(string filePath, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(CancellationToken cancellationToken);
}

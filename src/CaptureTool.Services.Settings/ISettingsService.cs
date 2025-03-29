namespace CaptureTool.Services.Settings;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Settings.Definitions;

public interface ISettingsService
{
    event Action<SettingDefinition[]>? SettingsChanged;

    T Get<T>(SettingDefinition<T> settingDefinition);
    bool IsSet(SettingDefinition settingDefinition);
    void Set<T, V>(T settingDefinition, V value) where T : SettingDefinition<V>;
    void Unset(SettingDefinition settingDefinition);
    void Unset(SettingDefinition[] settingDefinitions);

    Task InitializeAsync(string filePath);
    Task<bool> TrySaveAsync();
}

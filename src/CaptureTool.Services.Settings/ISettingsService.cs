namespace CaptureTool.Services.Settings;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services.Settings.Definitions;

public interface ISettingsService : IDisposable
{
    event Action<ICollection<SettingDefinition>>? SettingsChanged;

    T Get<T>(SettingDefinition<T> settingDefinition);
    bool IsSet(SettingDefinition settingDefinition);

    void Set(BoolSettingDefinition settingDefinition, bool newValue);
    void Set(DoubleSettingDefinition settingDefinition, double newValue);
    void Set(IntSettingDefinition settingDefinition, int newValue);
    void Set(StringSettingDefinition settingDefinition, string newValue);
    void Set(PointSettingDefinition settingDefinition, Point newValue);
    void Set(SizeSettingDefinition settingDefinition, Size newValue);
    void Set(StringArraySettingDefinition settingDefinition, string[] newValue);

    void Unset(SettingDefinition settingDefinition);
    void Unset(ICollection<SettingDefinition> settingDefinitions);

    Task InitializeAsync(string filePath, CancellationToken cancellationToken);
}

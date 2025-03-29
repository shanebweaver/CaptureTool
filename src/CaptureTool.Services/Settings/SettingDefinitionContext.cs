namespace CaptureTool.Services.Settings;

using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(SettingDefinition))]
[JsonSerializable(typeof(List<SettingDefinition>))]
[JsonSerializable(typeof(BoolSettingDefinition))]
[JsonSerializable(typeof(DoubleSettingDefinition))]
[JsonSerializable(typeof(IntSettingDefinition))]
[JsonSerializable(typeof(StringSettingDefinition))]
[JsonSerializable(typeof(PointSettingDefinition))]
[JsonSerializable(typeof(SizeSettingDefinition))]
[JsonSerializable(typeof(StringListSettingDefinition))]
public partial class SettingDefinitionContext : JsonSerializerContext { }

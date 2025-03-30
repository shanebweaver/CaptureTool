namespace CaptureTool.Services.Settings.Definitions;

using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(SettingDefinition))]
[JsonSerializable(typeof(List<SettingDefinition>))]
[JsonSerializable(typeof(BoolSettingDefinition))]
[JsonSerializable(typeof(DoubleSettingDefinition))]
[JsonSerializable(typeof(IntSettingDefinition))]
[JsonSerializable(typeof(StringSettingDefinition))]
public partial class SettingDefinitionContext : JsonSerializerContext { }

using CaptureTool.Application.Abstractions.Settings.Definitions;
using System.Text.Json.Serialization;

namespace CaptureTool.Application.Abstractions.Settings;

[JsonSerializable(typeof(SettingDefinition))]
[JsonSerializable(typeof(List<SettingDefinition>))]
[JsonSerializable(typeof(BoolSettingDefinition))]
[JsonSerializable(typeof(DoubleSettingDefinition))]
[JsonSerializable(typeof(IntSettingDefinition))]
[JsonSerializable(typeof(StringSettingDefinition))]
[JsonSerializable(typeof(PointSettingDefinition))]
[JsonSerializable(typeof(SizeSettingDefinition))]
public sealed partial class SettingDefinitionContext : JsonSerializerContext { }

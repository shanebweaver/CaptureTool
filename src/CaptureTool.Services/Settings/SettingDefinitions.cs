using System.Drawing;
using System.Text.Json.Serialization;

namespace CaptureTool.Services.Settings;


[JsonConverter(typeof(SettingDefinitionConverter))]
public abstract class SettingDefinition
{
    public string Key { get; }

    public SettingDefinition(string key) => Key = key;
}

public abstract class SettingDefinition<T> : SettingDefinition
{
    public T Value { get; }

    public SettingDefinition(string key, T value) : base(key) =>
        Value = value;
}

public class StringSettingDefinition : SettingDefinition<string>
{
    public StringSettingDefinition(string key, string value) : base(key, value) { }
}

public class StringListSettingDefinition : SettingDefinition<string[]>
{
    public StringListSettingDefinition(string key, string[] value) : base(key, value) { }
}

public class IntSettingDefinition : SettingDefinition<int>
{
    public IntSettingDefinition(string key, int value) : base(key, value) { }
}

public class DoubleSettingDefinition : SettingDefinition<double>
{
    public DoubleSettingDefinition(string key, double value) : base(key, value) { }
}

public class BoolSettingDefinition : SettingDefinition<bool>
{
    public BoolSettingDefinition(string key, bool value) : base(key, value) { }
}

public class PointSettingDefinition : SettingDefinition<Point>
{
    public PointSettingDefinition(string key, Point value) : base(key, value) { }
}

public class SizeSettingDefinition : SettingDefinition<Size>
{
    public SizeSettingDefinition(string key, Size value) : base(key, value) { }
}

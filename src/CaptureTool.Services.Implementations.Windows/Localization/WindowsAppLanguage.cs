using CaptureTool.Services.Interfaces.Localization;

namespace CaptureTool.Services.Implementations.Windows.Localization;

public readonly struct WindowsAppLanguage : IAppLanguage
{
    public string Value { get; }

    public WindowsAppLanguage(string value)
    {
        Value = value;
    }

    public override bool Equals(object? obj)
    {
        return obj is WindowsAppLanguage other && this == other;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(WindowsAppLanguage left, WindowsAppLanguage right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(WindowsAppLanguage left, WindowsAppLanguage right)
    {
        return !(left == right);
    }
}
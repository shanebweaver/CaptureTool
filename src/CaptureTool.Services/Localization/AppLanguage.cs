namespace CaptureTool.Services.Localization;

public readonly struct AppLanguage
{
    public string Value { get; }

    public AppLanguage(string value)
    {
        Value = value;
    }

    public override bool Equals(object? obj)
    {
        return obj is AppLanguage other && this == other;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static bool operator ==(AppLanguage left, AppLanguage right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(AppLanguage left, AppLanguage right)
    {
        return !(left == right);
    }
}
namespace CaptureTool.Infrastructure.Interfaces.Localization;

public interface IAppLanguage
{
    string Value { get; }

    bool Equals(object? obj);
    int GetHashCode();
}
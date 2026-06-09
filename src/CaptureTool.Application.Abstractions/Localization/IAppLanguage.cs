namespace CaptureTool.Application.Abstractions.Localization;

public interface IAppLanguage
{
    string Value { get; }

    bool Equals(object? obj);
    int GetHashCode();
}
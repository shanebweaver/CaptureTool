namespace CaptureTool.Application.Abstractions.Ai;

/// <summary>Cheap platform requirements. These checks never activate a model or download a runtime.</summary>
public interface IAiDeviceSupport
{
    bool WindowsAi { get; }
    bool FoundryCpu { get; }
}

namespace CaptureTool.Application.Abstractions.Activation;

public sealed record LaunchNavigationTarget(
    object Route,
    object? Parameter = null,
    bool ClearHistory = true);

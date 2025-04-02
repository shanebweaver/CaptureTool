namespace CaptureTool.Services.Navigation;

public readonly struct NavigationRequest
{
    public string Key { get; }
    public object? Parameter { get; }
    public bool IsBackNavigation { get; }

    public NavigationRequest(string key, object? parameter = null, bool isBackNavigation = false)
    {
        Key = key;
        Parameter = parameter;
        IsBackNavigation = isBackNavigation;
    }
}
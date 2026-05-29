namespace CaptureTool.FeatureManagement;

public sealed class FeatureFlag
{
    internal FeatureFlag(string id, bool isEnabled)
    {
        Id = id;
        IsEnabled = isEnabled;
    }

    internal string Id { get; }

    internal bool IsEnabled { get; }
}

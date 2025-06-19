namespace CaptureTool.FeatureManagement;

public sealed partial class FeatureFlag
{
    internal FeatureFlag(string id)
    {
        Id = id;
    }

    public string Id { get; }
}

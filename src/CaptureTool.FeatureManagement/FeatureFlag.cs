namespace CaptureTool.FeatureManagement;

public sealed partial class FeatureFlag
{
    public FeatureFlag(string id)
    {
        Id = id;
    }

    public string Id { get; }
}

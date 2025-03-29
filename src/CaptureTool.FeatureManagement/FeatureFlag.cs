namespace CaptureTool.FeatureManagement;

public sealed partial class FeatureFlag
{
    internal FeatureFlag(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

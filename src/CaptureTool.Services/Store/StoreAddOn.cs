namespace CaptureTool.Services.Store;

public sealed partial class StoreAddOn
{
    public string Id { get; }
    public bool IsOwned { get; }

    public StoreAddOn(string id, bool isOwned)
    {
        Id = id;
        IsOwned = isOwned;
    }
}

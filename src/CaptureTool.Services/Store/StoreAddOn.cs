namespace CaptureTool.Services.Store;

public sealed partial class StoreAddOn
{
    public string Id { get; }
    public bool IsOwned { get; }
    public string Price { get; }

    public StoreAddOn(string id, bool isOwned, string price)
    {
        Id = id;
        IsOwned = isOwned;
        Price = price;
    }
}

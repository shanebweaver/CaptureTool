using CaptureTool.Services.Interfaces.Store;

namespace CaptureTool.Services.Implementations.Windows.Store;

public sealed partial class WindowsStoreAddOn : IStoreAddOn
{
    public string Id { get; }
    public bool IsOwned { get; }
    public string Price { get; }
    public Uri? LogoImage { get; }

    public WindowsStoreAddOn(string id, bool isOwned, string price, Uri? logoImage)
    {
        Id = id;
        IsOwned = isOwned;
        Price = price;
        LogoImage = logoImage;
    }
}

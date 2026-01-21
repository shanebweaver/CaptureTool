namespace CaptureTool.Infrastructure.Interfaces.Store;

public interface IStoreAddOn
{
    string Id { get; }
    bool IsOwned { get; }
    Uri? LogoImage { get; }
    string Price { get; }
}
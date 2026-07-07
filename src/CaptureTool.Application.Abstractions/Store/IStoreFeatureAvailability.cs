namespace CaptureTool.Application.Abstractions.Store;

public interface IStoreFeatureAvailability
{
    bool IsStoreEnabled { get; }
}

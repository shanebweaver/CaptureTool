using CaptureTool.Application.Abstractions.Store;

namespace CaptureTool.Application.Abstractions.Features.Store.GetChromaKeyAddOn;

public sealed record GetChromaKeyAddOnResponse(IStoreAddOn? AddOn);

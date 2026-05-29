using CaptureTool.Infrastructure.Abstractions.Store;

namespace CaptureTool.Application.Features.Store.GetChromaKeyAddOn;

public sealed record GetChromaKeyAddOnResponse(IStoreAddOn? AddOn);

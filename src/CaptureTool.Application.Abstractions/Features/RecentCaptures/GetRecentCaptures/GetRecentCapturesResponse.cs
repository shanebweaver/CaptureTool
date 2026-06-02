namespace CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;

public sealed record GetRecentCapturesResponse(IReadOnlyList<RecentCapture> Captures);

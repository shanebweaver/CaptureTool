namespace CaptureTool.Application.Features.RecentCaptures.GetRecentCaptures;

public sealed record GetRecentCapturesResponse(IReadOnlyList<RecentCapture> Captures);

namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;

public sealed record GetRecentCapturesResponse(IReadOnlyList<RecentCapture> Captures);

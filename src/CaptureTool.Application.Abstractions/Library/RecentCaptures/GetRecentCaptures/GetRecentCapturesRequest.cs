namespace CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;

public sealed record GetRecentCapturesRequest(int Skip = 0, int Take = 5);

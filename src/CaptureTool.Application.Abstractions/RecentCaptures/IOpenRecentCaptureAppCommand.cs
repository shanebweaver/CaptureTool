using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.RecentCaptures;

public interface IOpenRecentCaptureAppCommand : IConditionalAppCommand<string>
{
}
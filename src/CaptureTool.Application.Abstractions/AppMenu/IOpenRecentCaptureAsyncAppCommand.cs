using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.AppMenu;

public interface IOpenRecentCaptureAsyncAppCommand : IAsyncAppCommand<string>
{
}
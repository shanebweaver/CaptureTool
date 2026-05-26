using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.Settings;

public interface ISettingsUpdateVideoMetadataAutoSaveAppCommand : IAsyncAppCommand<bool>
{
}

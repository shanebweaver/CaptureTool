using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.Settings;

public interface ISettingsUpdateImageAutoSaveAppCommand : IAsyncAppCommand<bool> { }

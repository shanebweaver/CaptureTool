using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Abstractions.AudioEdit;

public interface IOpenAudioEditPageAppCommand : IAppCommand<IAudioFile>
{
}

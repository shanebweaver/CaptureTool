using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.AudioEdit;

public interface ICopyAudioFileAppCommand : IAsyncConditionalAppCommand<string>
{
}
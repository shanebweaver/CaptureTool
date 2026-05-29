using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Features.VideoEdit.OpenVideoEditPage;

public sealed record OpenVideoEditPageRequest(IVideoFile VideoFile);

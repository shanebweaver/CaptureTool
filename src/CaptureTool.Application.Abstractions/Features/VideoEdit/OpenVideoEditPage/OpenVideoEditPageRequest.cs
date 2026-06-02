using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;

public sealed record OpenVideoEditPageRequest(IVideoFile VideoFile);

using CaptureTool.Domain.Capture.Abstractions.Files;

namespace CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;

public sealed record OpenImageEditPageRequest(IImageFile ImageFile);

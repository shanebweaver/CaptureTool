using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;

public sealed record OpenImageEditPageRequest(IImageFile ImageFile);

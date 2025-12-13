using CaptureTool.Core.Interfaces.Actions.VideoEdit;
using CaptureTool.Common.Commands.Extensions;

namespace CaptureTool.Core.Implementations.Actions.VideoEdit;

public sealed partial class VideoEditActions : IVideoEditActions
{
    private readonly IVideoEditSaveAction _save;
    private readonly IVideoEditCopyAction _copy;

    public VideoEditActions(
        IVideoEditSaveAction save,
        IVideoEditCopyAction copy)
    {
        _save = save;
        _copy = copy;
    }

    public Task SaveAsync(string videoPath, CancellationToken ct) => _save.SaveAsync(videoPath, ct);
    public Task CopyAsync(string videoPath, CancellationToken ct) => _copy.CopyAsync(videoPath, ct);
}

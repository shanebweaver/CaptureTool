namespace CaptureTool.Application.Abstractions.Edit.Image.Description;

public enum ImageDescriptionStatus
{
    Success,
    Cancelled,
    NotReady,
    NotSupported,
    BlockedByPolicy,
    BlockedByContentSafety,
    TooMuchText,
    Failed
}

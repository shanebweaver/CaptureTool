namespace CaptureTool.Presentation.Shell;

public sealed record BackgroundActivityItemViewModel
{
    public BackgroundActivityItemViewModel(
        string title,
        string detail,
        bool isActive,
        bool isAttention,
        bool isDeterminate = false,
        double progress = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        if (!double.IsFinite(progress) || progress is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(progress));
        }

        if (isDeterminate && !isActive)
        {
            throw new ArgumentException(
                "Only active background work can expose determinate progress.",
                nameof(isDeterminate));
        }

        Title = title;
        Detail = detail;
        IsActive = isActive;
        IsAttention = isAttention;
        IsWaiting = !isActive && !isAttention;
        IsDeterminate = isDeterminate;
        IsIndeterminate = isActive && !isDeterminate;
        ShowProgress = isActive;
        Progress = progress;
    }

    public string Title { get; }

    public string Detail { get; }

    public bool IsActive { get; }

    public bool IsAttention { get; }

    public bool IsWaiting { get; }

    public bool IsDeterminate { get; }

    public bool IsIndeterminate { get; }

    public bool ShowProgress { get; }

    public double Progress { get; }
}

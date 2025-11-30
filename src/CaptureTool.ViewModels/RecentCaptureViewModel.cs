using CaptureTool.Common;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.ViewModels;

public sealed partial class RecentCaptureViewModel : ViewModelBase
{
    public string FilePath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string FileName
    {
        get => field;
        private set => Set(ref field, value);
    }

    public CaptureFileType CaptureFileType
    {
        get => field;
        private set => Set(ref field, value);
    }

    public RecentCaptureViewModel(string temporaryFilePath)
    {
        FilePath = temporaryFilePath;
        FileName = GetRelativeCaptureLabel(temporaryFilePath);
        CaptureFileType = DetectFileType(temporaryFilePath);
    }

    private static string GetRelativeCaptureLabel(string temporaryFilePath)
    {
        var info = new FileInfo(temporaryFilePath);
        DateTime timestamp = info.LastWriteTime;

        var now = DateTime.Now;
        var date = timestamp.Date;
        var today = now.Date;
        var yesterday = today.AddDays(-1);

        // --- NEW: super-relative "minutes ago" / "hours ago" ---
        if (date == today)
        {
            var diff = now - timestamp;

            if (diff.TotalMinutes < 1)
                return "Just now";

            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} minute{(diff.TotalMinutes < 2 ? "" : "s")} ago";

            if (diff.TotalHours < 3)
                return $"{(int)diff.TotalHours} hour{(diff.TotalHours < 2 ? "" : "s")} ago";

            // Past 3 hours → use longer form: "Today @ 4:30 PM"
            string timeToday = timestamp.ToString("h:mm tt");
            return $"Today @ {timeToday}";
        }

        // --- Yesterday ---
        if (date == yesterday)
        {
            string time = timestamp.ToString("h:mm tt");
            return $"Yesterday @ {time}";
        }

        // --- Within last week ---
        if (date > today.AddDays(-7))
        {
            return $"{timestamp:dddd} @ {timestamp:h:mm tt}";
        }

        // --- Earlier this year ---
        if (date.Year == now.Year)
        {
            return $"{timestamp:MMM d} @ {timestamp:h:mm tt}";
        }

        // --- Older ---
        return $"{timestamp:MMM d, yyyy} @ {timestamp:h:mm tt}";
    }


    private static CaptureFileType DetectFileType(string filePath)
    {
        return Path.GetExtension(filePath) switch
        {
            ".png" => CaptureFileType.Image,
            ".mp4" => CaptureFileType.Video,
            _ => CaptureFileType.Unknown,
        };
    }
}

using CaptureTool.Services.Feedback;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.System;

namespace CaptureTool.Services.Windows.Feedback;

public sealed partial class WindowsFeedbackService : IFeedbackService
{
    private const string FeedbackHubProtocol = "feedback-hub:?contextid=1206Weaver.65288EF293C1E_65xc5fd1yxhdr";

    public async Task<bool> IsFeedbackSupportedAsync()
    {
        try
        {
            var uri = new Uri(FeedbackHubProtocol);
            var options = new LauncherOptions
            {
                TreatAsUntrusted = false
            };

            // This just checks if there's an app registered to handle the URI
            var supportStatus = await Launcher.QueryUriSupportAsync(uri, LaunchQuerySupportType.Uri);

            return supportStatus == LaunchQuerySupportStatus.Available;
        }
        catch
        {
            return false;
        }
    }

    public async Task ShowFeedbackUIAsync()
    {
        bool isAvailable = await IsFeedbackSupportedAsync();
        if (isAvailable)
        {
            // Safe to launch
            Process.Start(new ProcessStartInfo
            {
                FileName = FeedbackHubProtocol,
                UseShellExecute = true
            });
        }
        else
        {
            // Show a message to the user
            Debug.WriteLine("Feedback Hub is not available on this system.");
        }
    }
}

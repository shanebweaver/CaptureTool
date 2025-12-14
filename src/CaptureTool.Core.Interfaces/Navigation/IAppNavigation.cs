using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Storage;

namespace CaptureTool.Core.Interfaces.Navigation;

/// <summary>
/// Provides navigation services for the application's main flows and pages.
/// </summary>
public interface IAppNavigation
{
    /// <summary>
    /// Gets a value indicating whether the navigation can go back to a previous page.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Navigates back to the previous page, or to the home page if there is no navigation history.
    /// </summary>
    void GoBackOrGoHome();

    /// <summary>
    /// Navigates back to the main application window.
    /// </summary>
    void GoBackToMainWindow();

    /// <summary>
    /// Navigates to the home page.
    /// </summary>
    void GoHome();

    /// <summary>
    /// Navigates to the about page.
    /// </summary>
    void GoToAbout();

    /// <summary>
    /// Navigates to the add-ons page.
    /// </summary>
    void GoToAddOns();

    /// <summary>
    /// Navigates to the error page with the specified exception details.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    void GoToError(Exception exception);

    /// <summary>
    /// Navigates to the image capture flow with the specified options.
    /// </summary>
    /// <param name="captureOptions">The options for capturing the image.</param>
    /// <param name="clearHistory">If true, clears the navigation history.</param>
    void GoToImageCapture(CaptureOptions captureOptions, bool clearHistory = false);

    /// <summary>
    /// Navigates to the image edit page for the specified image file.
    /// </summary>
    /// <param name="imageFile">The image file to edit.</param>
    void GoToImageEdit(IImageFile imageFile);

    /// <summary>
    /// Navigates to the loading page.
    /// </summary>
    void GoToLoading();

    /// <summary>
    /// Navigates to the settings page.
    /// </summary>
    void GoToSettings();

    /// <summary>
    /// Navigates to the video capture flow with the specified arguments.
    /// </summary>
    /// <param name="captureargs">The arguments for video capture.</param>
    void GoToVideoCapture(NewCaptureArgs captureargs);

    /// <summary>
    /// Navigates to the video edit page for the specified video file.
    /// </summary>
    /// <param name="videoFile">The video file to edit.</param>
    void GoToVideoEdit(IVideoFile videoFile);

    /// <summary>
    /// Attempts to navigate back to the previous page.
    /// </summary>
    /// <returns>True if navigation was successful; otherwise, false.</returns>
    bool TryGoBack();
}
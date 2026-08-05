using CaptureTool.Application.Abstractions.Capture.Audio;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Navigation;
using Moq;

namespace CaptureTool.Application.Tests;

internal static class TestNavigationCoordinator
{
    public static INavigationCoordinator Create(
        INavigationService navigationService,
        IEditSessionGuard? editSessionGuard = null,
        IAudioCaptureNavigationGuard? audioCaptureNavigationGuard = null)
    {
        if (editSessionGuard is null)
        {
            var allowEditSessionGuard = new Mock<IEditSessionGuard>();
            allowEditSessionGuard
                .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            editSessionGuard = allowEditSessionGuard.Object;
        }

        if (audioCaptureNavigationGuard is null)
        {
            var allowAudioCaptureNavigationGuard = new Mock<IAudioCaptureNavigationGuard>();
            allowAudioCaptureNavigationGuard
                .Setup(guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            audioCaptureNavigationGuard = allowAudioCaptureNavigationGuard.Object;
        }

        return new NavigationCoordinator(
            navigationService,
            editSessionGuard,
            audioCaptureNavigationGuard);
    }
}

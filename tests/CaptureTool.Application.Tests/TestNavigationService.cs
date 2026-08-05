using CaptureTool.Application.Abstractions.Navigation;
using Moq;

namespace CaptureTool.Application.Tests;

internal static class TestNavigationService
{
    public static void AcceptAll(Mock<INavigationService> navigation)
    {
        navigation
            .Setup(service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Accepted);
        navigation
            .Setup(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Accepted);
        navigation
            .Setup(service => service.TryGoBackToAsync(
                It.IsAny<Func<INavigationRequest, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Accepted);
    }
}

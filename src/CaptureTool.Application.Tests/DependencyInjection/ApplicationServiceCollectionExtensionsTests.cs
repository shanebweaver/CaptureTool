using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.SettingsPage.OpenSettingsPage;
using CaptureTool.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Application.Tests.DependencyInjection;

[TestClass]
public sealed class ApplicationServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddApplicationServices_ShouldRegisterCoreApplicationServices()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddApplicationServices();

        Assert.AreSame(services, result);
        AssertHasRegistration<IUseCaseExecutor, UseCaseExecutor>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IActiveEditSessionService, ActiveEditSessionService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IEditSessionGuard, EditSessionGuard>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IAudioCaptureHandler, AudioCaptureHandler>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IOpenSelectionOverlayUseCase, OpenSelectionOverlayUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IOpenSettingsPageUseCase, OpenSettingsPageUseCase>(services, ServiceLifetime.Transient);
    }

    private static void AssertHasRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == lifetime));
    }
}

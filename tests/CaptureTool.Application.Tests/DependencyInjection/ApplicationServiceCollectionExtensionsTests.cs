using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Features.AudioCapture;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Features.VideoCapture;
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
        AssertHasRegistration<AudioCaptureWorkflow, AudioCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAudioCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAudioCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<VideoCaptureWorkflow, VideoCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IVideoCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IVideoCaptureState>(services, ServiceLifetime.Singleton);
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

    private static void AssertHasFactoryRegistration<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        Assert.IsTrue(services.Any(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationFactory is not null &&
            descriptor.Lifetime == lifetime));
    }
}

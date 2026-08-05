using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Abstractions.Capture.Image.CaptureImage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Capture.Video.StartVideoCapture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Activation;
using CaptureTool.Application.Ai;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Capture.Image;
using CaptureTool.Application.Capture.Image.CaptureAllScreensImage;
using CaptureTool.Application.Capture.Image.CaptureImage;
using CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Capture.Video;
using CaptureTool.Application.Capture.Video.StartVideoCapture;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Library.RecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.ClearRecentCaptures;
using CaptureTool.Application.Library.RecentCaptures.RemoveRecentCapture;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.Settings.OpenSettingsPage;
using CaptureTool.Application.Storage;
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
        AssertHasRegistration<IAiFeatureConsentService, AiFeatureConsentService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IApplicationStartupInitializer, ApplicationStartupInitializer>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ILaunchNavigationTargetProvider, DefaultLaunchNavigationTargetProvider>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IActiveEditSessionService, ActiveEditSessionService>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IEditSessionGuard, EditSessionGuard>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<INavigationCoordinator, NavigationCoordinator>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IScratchArtifactStore, ScratchArtifactStore>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IRecentCapturesChangeNotifier, RecentCapturesChangeNotifier>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IClearRecentCapturesUseCase, ClearRecentCapturesUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<IRemoveRecentCaptureUseCase, RemoveRecentCaptureUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<AudioCaptureWorkflow, AudioCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAudioCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IAudioCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICancelAudioCaptureUseCase, CancelAudioCaptureUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<VideoCaptureWorkflow, VideoCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IVideoCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IVideoCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<IStartVideoCaptureUseCase, StartVideoCaptureUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<ImageCaptureWorkflow, ImageCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IImageCaptureWorkflow>(services, ServiceLifetime.Singleton);
        AssertHasFactoryRegistration<IImageCaptureState>(services, ServiceLifetime.Singleton);
        AssertHasRegistration<ICaptureAllScreensImageUseCase, CaptureAllScreensImageUseCase>(services, ServiceLifetime.Transient);
        AssertHasRegistration<ICaptureImageUseCase, CaptureImageUseCase>(services, ServiceLifetime.Transient);
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

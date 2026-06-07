using CaptureTool.Domain.Capture;
using CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Capture.Windows.V2;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests.DependencyInjection;

[TestClass]
public sealed class WindowsCaptureInfrastructureServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddWindowsCaptureDomains_RegistersCaptureServices()
    {
        var services = new ServiceCollection();

        services.AddWindowsCaptureDomains();

        services.ShouldContainSingleton<IScreenCapture, WindowsScreenCapture>();
        services.ShouldContainSingleton<IScreenRecorder, WindowsScreenRecorder>();
        services.ShouldContainSingleton<IAudioRecorder, WindowsAudioRecorder>();
    }

    [TestMethod]
    public void AddWindowsCaptureDomains_DefaultOptions_ResolvesExistingRecorder()
    {
        var services = new ServiceCollection();
        services.AddWindowsCaptureDomains();

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IScreenRecorder>().Should().BeOfType<WindowsScreenRecorder>();
    }

    [TestMethod]
    public void AddWindowsCaptureDomains_V2OptionOff_UsesCurrentRecorder()
    {
        var services = new ServiceCollection();
        services.AddWindowsCaptureDomains(options => options.UseCaptureV2ScreenRecorder = false);

        services.ShouldContainSingleton<IScreenRecorder, WindowsScreenRecorder>();
    }

    [TestMethod]
    public void AddWindowsCaptureDomains_V2OptionOn_UsesAdapterWithoutReplacingDefaultPath()
    {
        var services = new ServiceCollection();
        services.AddWindowsCaptureDomains(options => options.UseCaptureV2ScreenRecorder = true);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IScreenRecorder>().Should().BeOfType<CaptureV2ScreenRecorderAdapter>();
    }

    [TestMethod]
    public void AddWindowsCaptureDomains_V2OptionOn_CanUseDummyRecorderFactory()
    {
        var recorder = new DummyScreenRecorder();
        var services = new ServiceCollection();
        services.AddWindowsCaptureDomains(options =>
        {
            options.UseCaptureV2ScreenRecorder = true;
            options.CaptureV2ScreenRecorderFactory = _ => recorder;
        });

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IScreenRecorder>().Should().BeSameAs(recorder);
    }
}

file static class ServiceCollectionAssertions
{
    public static void ShouldContainSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(TService)
            && descriptor.ImplementationType == typeof(TImplementation)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}

file sealed class DummyScreenRecorder : IScreenRecorder
{
    public bool StartRecording(nint hMonitor, string outputPath, bool captureAudio = false) => true;

    public void StopRecording()
    {
    }

    public void PauseRecording()
    {
    }

    public void ResumeRecording()
    {
    }

    public void ToggleAudioCapture(bool enabled)
    {
    }

    public void SetVideoFrameCallback(VideoFrameCallback? callback)
    {
    }

    public void SetAudioSampleCallback(AudioSampleCallback? callback)
    {
    }
}

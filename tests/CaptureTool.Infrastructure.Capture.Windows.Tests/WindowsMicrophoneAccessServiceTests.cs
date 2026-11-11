using CaptureTool.Application.Abstractions.Capture;
using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace CaptureTool.Infrastructure.Capture.Windows.Tests;

[TestClass]
public sealed class WindowsMicrophoneAccessServiceTests
{
    [TestMethod]
    [DataRow(AppCapabilityAccessStatus.Allowed, MicrophoneAccessResult.Granted)]
    [DataRow(AppCapabilityAccessStatus.DeniedByUser, MicrophoneAccessResult.Declined)]
    public async Task UndecidedAccessRequestsNativeConsent(AppCapabilityAccessStatus answer, MicrophoneAccessResult expected)
    {
        int requests = 0;
        var service = new WindowsMicrophoneAccessService(() => AppCapabilityAccessStatus.UserPromptRequired,
            _ => { requests++; return Task.FromResult(answer); }, () => DeviceAccessStatus.Allowed);
        Assert.AreEqual(expected, await service.RequestAccessAsync());
        Assert.AreEqual(1, requests);
    }

    [TestMethod]
    [DataRow(AppCapabilityAccessStatus.DeniedByUser)]
    [DataRow(AppCapabilityAccessStatus.DeniedBySystem)]
    public async Task ExistingDenialRequiresSettingsWithoutRepeatingNativePrompt(AppCapabilityAccessStatus status)
    {
        var service = new WindowsMicrophoneAccessService(() => status,
            _ => throw new AssertFailedException("A previously blocked permission cannot be granted by repeating the native prompt."),
            () => DeviceAccessStatus.Allowed);
        Assert.AreEqual(MicrophoneAccessResult.SettingsRequired, await service.RequestAccessAsync());
    }

    [TestMethod]
    [DataRow(DeviceAccessStatus.Allowed, MicrophoneAccessResult.Granted)]
    [DataRow(DeviceAccessStatus.Unspecified, MicrophoneAccessResult.Granted)]
    [DataRow(DeviceAccessStatus.DeniedByUser, MicrophoneAccessResult.SettingsRequired)]
    [DataRow(DeviceAccessStatus.DeniedBySystem, MicrophoneAccessResult.SettingsRequired)]
    public async Task UnpackagedAppUsesDevicePrivacyStatus(DeviceAccessStatus status, MicrophoneAccessResult expected)
    {
        var service = new WindowsMicrophoneAccessService(() => AppCapabilityAccessStatus.NotDeclaredByApp,
            _ => throw new AssertFailedException("No per-app prompt is available."), () => status);
        Assert.AreEqual(expected, await service.RequestAccessAsync());
    }

    [TestMethod]
    public async Task DeviceWideDenialWinsAndAccessIsRecheckedAfterSettingsChange()
    {
        DeviceAccessStatus access = DeviceAccessStatus.DeniedBySystem;
        var service = new WindowsMicrophoneAccessService(() => AppCapabilityAccessStatus.Allowed,
            _ => throw new AssertFailedException("Already granted."), () => access);
        Assert.AreEqual(MicrophoneAccessResult.SettingsRequired, await service.RequestAccessAsync());
        access = DeviceAccessStatus.Allowed;
        Assert.AreEqual(MicrophoneAccessResult.Granted, await service.RequestAccessAsync());
    }

    [TestMethod]
    public async Task UnavailableCapabilityApiStillHonorsDeviceDenial()
    {
        var service = new WindowsMicrophoneAccessService(() => throw new COMException("API unavailable"),
            _ => throw new AssertFailedException("No prompt."), () => DeviceAccessStatus.DeniedByUser);
        Assert.AreEqual(MicrophoneAccessResult.SettingsRequired, await service.RequestAccessAsync());
    }

    [TestMethod]
    public async Task NativeAccessDeniedCannotFallBackToGrantedDeviceStatus()
    {
        var service = new WindowsMicrophoneAccessService(() => throw new COMException("Access denied", unchecked((int)0x80070005)),
            _ => throw new AssertFailedException("No prompt."), () => DeviceAccessStatus.Allowed);
        Assert.AreEqual(MicrophoneAccessResult.SettingsRequired, await service.RequestAccessAsync());
    }

    [TestMethod]
    public async Task CancellationRejectsLateNativeGrant()
    {
        using var cancellation = new CancellationTokenSource();
        var answer = new TaskCompletionSource<AppCapabilityAccessStatus>();
        var service = new WindowsMicrophoneAccessService(() => AppCapabilityAccessStatus.UserPromptRequired,
            _ => answer.Task, () => DeviceAccessStatus.Allowed);
        var request = service.RequestAccessAsync(cancellation.Token);
        await cancellation.CancelAsync();
        answer.SetResult(AppCapabilityAccessStatus.Allowed);
        await Assert.ThrowsAsync<OperationCanceledException>(() => request);
    }
}

using CaptureTool.Infrastructure.Windows.Windowing;

namespace CaptureTool.Infrastructure.Windows.Tests.Windowing;

[TestClass]
public sealed class MainWindowActivationServiceTests
{
    [TestMethod]
    public async Task WaitUntilActivatedAsync_CompletesWhenWindowBecomesActive()
    {
        MainWindowActivationService service = new();

        Task activation = service.WaitUntilActivatedAsync();
        Assert.IsFalse(activation.IsCompleted);

        service.SetActive(true);

        await activation.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task WaitUntilActivatedAsync_WaitsAgainAfterWindowIsDeactivated()
    {
        MainWindowActivationService service = new();
        service.SetActive(true);
        await service.WaitUntilActivatedAsync();

        service.SetActive(false);
        Task reactivation = service.WaitUntilActivatedAsync();
        Assert.IsFalse(reactivation.IsCompleted);

        service.SetActive(true);

        await reactivation.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

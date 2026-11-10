using CaptureTool.Presentation.Features.ImageEdit;
using FluentAssertions;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditOperationCoordinatorTests
{
    [TestMethod]
    public async Task StopAsync_WhenCancellationCallbackThrows_StillCancelsAndDrainsAllOperations()
    {
        using var coordinator = new ImageEditOperationCoordinator();
        using var first = coordinator.Start(ImageEditOperation.ImageDescription);
        using var second = coordinator.Start(ImageEditOperation.TextExtraction);
        var failure = new InvalidOperationException("Provider cancellation failed.");
        using var registration = first.Token.Register(() => throw failure);

        Task stopping = coordinator.StopAsync();

        first.Token.IsCancellationRequested.Should().BeTrue();
        second.Token.IsCancellationRequested.Should().BeTrue();
        stopping.IsCompleted.Should().BeFalse("cancellation failure must not bypass provider cleanup");
        first.Dispose();
        stopping.IsCompleted.Should().BeFalse();
        second.Dispose();

        Func<Task> complete = () => stopping.WaitAsync(TimeSpan.FromSeconds(5));
        var thrown = await complete.Should().ThrowAsync<AggregateException>();
        thrown.Which.Flatten().InnerExceptions.Should().Contain(failure);
    }

    [TestMethod]
    public async Task StopAsync_WaitsForCanceledAndSupersededOperationsToUnwind()
    {
        using var coordinator = new ImageEditOperationCoordinator();
        var superseded = coordinator.Start(ImageEditOperation.ImageDescription);
        var current = coordinator.Start(ImageEditOperation.ImageDescription);
        var extraction = coordinator.Start(ImageEditOperation.TextExtraction);

        Task stopping = coordinator.StopAsync();

        superseded.Token.IsCancellationRequested.Should().BeTrue();
        current.Token.IsCancellationRequested.Should().BeTrue();
        extraction.Token.IsCancellationRequested.Should().BeTrue();
        stopping.IsCompleted.Should().BeFalse();
        current.Dispose();
        extraction.Dispose();
        stopping.IsCompleted.Should().BeFalse("a canceled provider may still be releasing native resources");
        superseded.Dispose();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.StopAsync();
    }

    [TestMethod]
    public void Start_ShouldCancelPreviousGeneration_ForSameOperation()
    {
        using var coordinator = new ImageEditOperationCoordinator();
        using ImageEditOperationCoordinator.OperationLease first =
            coordinator.Start(ImageEditOperation.ForegroundExtraction);

        using ImageEditOperationCoordinator.OperationLease second =
            coordinator.Start(ImageEditOperation.ForegroundExtraction);

        first.Token.IsCancellationRequested.Should().BeTrue();
        first.IsCurrent.Should().BeFalse();
        second.Token.IsCancellationRequested.Should().BeFalse();
        second.IsCurrent.Should().BeTrue();
    }

    [TestMethod]
    public void Start_ShouldKeepDifferentOperationsIndependent()
    {
        using var coordinator = new ImageEditOperationCoordinator();
        using ImageEditOperationCoordinator.OperationLease foreground =
            coordinator.Start(ImageEditOperation.ForegroundExtraction);

        using ImageEditOperationCoordinator.OperationLease objectErase =
            coordinator.Start(ImageEditOperation.ObjectErase);

        foreground.IsCurrent.Should().BeTrue();
        foreground.Token.IsCancellationRequested.Should().BeFalse();
        objectErase.IsCurrent.Should().BeTrue();
        objectErase.Token.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public void DisposeLease_ShouldNotCompleteNewerGeneration()
    {
        using var coordinator = new ImageEditOperationCoordinator();
        ImageEditOperationCoordinator.OperationLease first =
            coordinator.Start(ImageEditOperation.ImageDescription);
        using ImageEditOperationCoordinator.OperationLease second =
            coordinator.Start(ImageEditOperation.ImageDescription);

        first.Dispose();

        second.IsCurrent.Should().BeTrue();
        second.Token.IsCancellationRequested.Should().BeFalse();
    }

    [TestMethod]
    public void Cancel_ShouldCancelOnlyRequestedOperation()
    {
        using var coordinator = new ImageEditOperationCoordinator();
        using ImageEditOperationCoordinator.OperationLease textExtraction =
            coordinator.Start(ImageEditOperation.TextExtraction);
        using ImageEditOperationCoordinator.OperationLease superResolution =
            coordinator.Start(ImageEditOperation.SuperResolution);

        coordinator.Cancel(ImageEditOperation.TextExtraction);

        textExtraction.Token.IsCancellationRequested.Should().BeTrue();
        textExtraction.IsCurrent.Should().BeFalse();
        superResolution.Token.IsCancellationRequested.Should().BeFalse();
        superResolution.IsCurrent.Should().BeTrue();
    }

    [TestMethod]
    public void Dispose_ShouldCancelAllOperations_AndRejectNewWork()
    {
        var coordinator = new ImageEditOperationCoordinator();
        using ImageEditOperationCoordinator.OperationLease textExtraction =
            coordinator.Start(ImageEditOperation.TextExtraction);
        using ImageEditOperationCoordinator.OperationLease objectExtraction =
            coordinator.Start(ImageEditOperation.ObjectExtraction);

        coordinator.Dispose();

        textExtraction.Token.IsCancellationRequested.Should().BeTrue();
        objectExtraction.Token.IsCancellationRequested.Should().BeTrue();
        Action start = () => coordinator.Start(ImageEditOperation.SuperResolution);
        start.Should().Throw<ObjectDisposedException>();
    }
}

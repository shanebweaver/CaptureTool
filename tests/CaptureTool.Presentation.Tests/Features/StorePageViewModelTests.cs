using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Presentation.Features.Store;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class StorePageViewModelTests
{
    [TestMethod]
    public async Task PurchaseChromaKeyAddOnCommand_WhenPurchaseSucceeds_RefreshesAndOwnsAddOn()
    {
        IStoreAddOn initialAddOn = CreateAddOn(false, "$4.99");
        IStoreAddOn delayedStoreAddOn = CreateAddOn(false, "$4.99");
        var getAddOn = new Mock<IGetChromaKeyAddOnUseCase>();
        getAddOn
            .SetupSequence(useCase => useCase.ExecuteAsync(
                It.IsAny<GetChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGetResponse(initialAddOn))
            .ReturnsAsync(CreateGetResponse(delayedStoreAddOn));
        var purchase = new Mock<IPurchaseChromaKeyAddOnUseCase>();
        purchase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<PurchaseChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePurchaseResponse(true));
        StorePageViewModel viewModel = CreateViewModel(purchase.Object, getAddOn.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.PurchaseChromaKeyAddOnCommand.ExecuteAsync(null);

        viewModel.IsChromaKeyAddOnOwned.Should().BeTrue();
        viewModel.IsChromaKeyAddOnAvailable.Should().BeFalse();
        viewModel.CanPurchaseChromaKeyAddOn.Should().BeFalse();
        viewModel.ChromaKeyAddOnPrice.Should().Be("Owned");
        viewModel.HasChromaKeyAddOnPurchaseFailure.Should().BeFalse();
        getAddOn.Verify(
            useCase => useCase.ExecuteAsync(
                It.IsAny<GetChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task PurchaseChromaKeyAddOnCommand_WhilePurchaseIsRunning_DisablesDuplicateAttempt()
    {
        var purchaseCompletion = new TaskCompletionSource<UseCaseResponse<PurchaseChromaKeyAddOnResponse>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var purchase = new Mock<IPurchaseChromaKeyAddOnUseCase>();
        purchase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<PurchaseChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(purchaseCompletion.Task);
        var getAddOn = CreateGetAddOnUseCase(CreateAddOn(false, "$4.99"));
        StorePageViewModel viewModel = CreateViewModel(purchase.Object, getAddOn.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        Task purchaseTask = viewModel.PurchaseChromaKeyAddOnCommand.ExecuteAsync(null);

        viewModel.IsPurchasingChromaKeyAddOn.Should().BeTrue();
        viewModel.CanPurchaseChromaKeyAddOn.Should().BeFalse();
        viewModel.ChromaKeyAddOnPurchaseButtonText.Should().BeEmpty();
        viewModel.PurchaseChromaKeyAddOnCommand.CanExecute(null).Should().BeFalse();
        purchase.Verify(
            useCase => useCase.ExecuteAsync(
                It.IsAny<PurchaseChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        purchaseCompletion.SetResult(CreatePurchaseResponse(false));
        await purchaseTask;
        viewModel.IsPurchasingChromaKeyAddOn.Should().BeFalse();
        viewModel.ChromaKeyAddOnPurchaseButtonText.Should().Be("$4.99");
    }

    [TestMethod]
    public async Task PurchaseChromaKeyAddOnCommand_WhenRetrySucceeds_ClearsFailureAndOwnsAddOn()
    {
        var purchase = new Mock<IPurchaseChromaKeyAddOnUseCase>();
        purchase
            .SetupSequence(useCase => useCase.ExecuteAsync(
                It.IsAny<PurchaseChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePurchaseResponse(false))
            .ReturnsAsync(CreatePurchaseResponse(true));
        var getAddOn = new Mock<IGetChromaKeyAddOnUseCase>();
        getAddOn
            .SetupSequence(useCase => useCase.ExecuteAsync(
                It.IsAny<GetChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGetResponse(CreateAddOn(false, "$4.99")))
            .ReturnsAsync(CreateGetResponse(null));
        StorePageViewModel viewModel = CreateViewModel(purchase.Object, getAddOn.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.PurchaseChromaKeyAddOnCommand.ExecuteAsync(null);

        viewModel.HasChromaKeyAddOnPurchaseFailure.Should().BeTrue();
        viewModel.CanPurchaseChromaKeyAddOn.Should().BeTrue();

        await viewModel.PurchaseChromaKeyAddOnCommand.ExecuteAsync(null);

        viewModel.HasChromaKeyAddOnPurchaseFailure.Should().BeFalse();
        viewModel.IsChromaKeyAddOnOwned.Should().BeTrue();
        viewModel.CanPurchaseChromaKeyAddOn.Should().BeFalse();
        purchase.Verify(
            useCase => useCase.ExecuteAsync(
                It.IsAny<PurchaseChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task PurchaseChromaKeyAddOnCommand_WhenCanceled_ShowsRetryableFailure()
    {
        var purchase = new Mock<IPurchaseChromaKeyAddOnUseCase>();
        purchase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<PurchaseChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<PurchaseChromaKeyAddOnResponse>.Cancelled());
        var getAddOn = CreateGetAddOnUseCase(CreateAddOn(false, "$4.99"));
        StorePageViewModel viewModel = CreateViewModel(purchase.Object, getAddOn.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.PurchaseChromaKeyAddOnCommand.ExecuteAsync(null);

        viewModel.HasChromaKeyAddOnPurchaseFailure.Should().BeTrue();
        viewModel.IsPurchasingChromaKeyAddOn.Should().BeFalse();
        viewModel.CanPurchaseChromaKeyAddOn.Should().BeTrue();
    }

    private static StorePageViewModel CreateViewModel(
        IPurchaseChromaKeyAddOnUseCase purchase,
        IGetChromaKeyAddOnUseCase getAddOn)
    {
        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns<string>(key => key switch
            {
                "AddOns_ItemUnknown" => "Unknown",
                "AddOns_ItemNotAvailable" => "Unavailable",
                "AddOns_ItemOwned" => "Owned",
                _ => key
            });
        var cancellation = new Mock<ICancellationService>();
        cancellation
            .Setup(service => service.GetLinkedCancellationTokenSource(
                It.IsAny<CancellationToken?>()))
            .Returns<CancellationToken?>(token =>
                CancellationTokenSource.CreateLinkedTokenSource(token ?? CancellationToken.None));
        return new StorePageViewModel(
            Mock.Of<ILeaveStorePageUseCase>(),
            purchase,
            getAddOn,
            localization.Object,
            cancellation.Object);
    }

    private static Mock<IGetChromaKeyAddOnUseCase> CreateGetAddOnUseCase(IStoreAddOn addOn)
    {
        var getAddOn = new Mock<IGetChromaKeyAddOnUseCase>();
        getAddOn
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<GetChromaKeyAddOnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateGetResponse(addOn));
        return getAddOn;
    }

    private static IStoreAddOn CreateAddOn(bool isOwned, string price)
    {
        return Mock.Of<IStoreAddOn>(addOn =>
            addOn.IsOwned == isOwned &&
            addOn.Price == price);
    }

    private static UseCaseResponse<GetChromaKeyAddOnResponse> CreateGetResponse(IStoreAddOn? addOn)
        => UseCaseResponse<GetChromaKeyAddOnResponse>.Success(new(addOn));

    private static UseCaseResponse<PurchaseChromaKeyAddOnResponse> CreatePurchaseResponse(bool purchased)
        => UseCaseResponse<PurchaseChromaKeyAddOnResponse>.Success(new(purchased));

    public TestContext TestContext { get; set; } = null!;
}

using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shell.Error.RestartApplication;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Presentation.Shell;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Shell;

[TestClass]
public sealed class ErrorPageViewModelTests
{
    [TestMethod]
    public async Task RestartAppCommand_WhenRestartFails_ShowsRecoverableFailure()
    {
        var restartUseCase = new Mock<IRestartApplicationUseCase>();
        restartUseCase
            .Setup(useCase => useCase.CanExecute(It.IsAny<RestartApplicationRequest>()))
            .Returns(true);
        restartUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<RestartApplicationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<RestartApplicationResponse>.Success(new(false)));
        var exitUseCase = new Mock<IExitApplicationUseCase>();
        exitUseCase
            .Setup(useCase => useCase.CanExecute(It.IsAny<ExitApplicationRequest>()))
            .Returns(true);
        var viewModel = new ErrorPageViewModel(restartUseCase.Object, exitUseCase.Object);

        await viewModel.RestartAppCommand.ExecuteAsync(null);

        viewModel.HasRestartFailed.Should().BeTrue();
        viewModel.RestartAppCommand.CanExecute(null).Should().BeTrue();
        viewModel.ExitAppCommand.CanExecute(null).Should().BeTrue();
    }
}

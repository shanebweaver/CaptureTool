using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Presentation.Shared.Commands;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class UseCaseCommandExtensionsTests
{
    [TestMethod]
    public async Task ToAsyncRelayCommand_ShouldLogError_AndComplete_WhenUseCaseThrows()
    {
        var exception = new InvalidOperationException("Command failed.");
        var telemetry = new Mock<ITelemetryService>();
        var useCase = new ThrowingUseCase(exception);
        var command = useCase.ToAsyncRelayCommand(() => new TestRequest(), telemetry.Object, "TestActivity");

        command.Execute(null);
        await command.ExecutionTask!;

        telemetry.Verify(
            service => service.ActivityError(
                "TestActivity",
                exception,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ToAsyncRelayCommand_ShouldLogCanceled_AndComplete_WhenUseCaseCancels()
    {
        var exception = new OperationCanceledException("Command canceled.");
        var telemetry = new Mock<ITelemetryService>();
        var useCase = new ThrowingUseCase(exception);
        var command = useCase.ToAsyncRelayCommand(() => new TestRequest(), telemetry.Object, "TestActivity");

        command.Execute(null);
        await command.ExecutionTask!;

        telemetry.Verify(
            service => service.ActivityCanceled("TestActivity", "Command canceled."),
            Times.Once);
        telemetry.Verify(
            service => service.ActivityError(
                It.IsAny<string>(),
                It.IsAny<Exception>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    private sealed record TestRequest;

    private sealed record TestResponse;

    private sealed class ThrowingUseCase : IUseCase<TestRequest, TestResponse>
    {
        private readonly Exception _exception;

        public ThrowingUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task<TestResponse> ExecuteAsync(TestRequest request, CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}

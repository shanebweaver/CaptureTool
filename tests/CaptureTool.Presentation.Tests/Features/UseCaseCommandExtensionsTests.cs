using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Presentation.Shared.Commands;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class UseCaseCommandExtensionsTests
{
    [TestMethod]
    public async Task ToAsyncRelayCommand_ShouldTrackSucceededAction_WhenUseCaseSucceeds()
    {
        var telemetry = new Mock<ITelemetryService>();
        var useCase = new SuccessfulUseCase();
        var command = useCase.ToAsyncRelayCommand(() => new TestRequest(), telemetry.Object, "TestActivity");

        command.Execute(null);
        await command.ExecutionTask!;

        VerifyAction(telemetry, TelemetryOutcomes.Succeeded);
    }

    [TestMethod]
    public async Task ToAsyncRelayCommand_ShouldTrackFailedAction_WhenUseCaseThrows()
    {
        var exception = new InvalidOperationException("Command failed.");
        var telemetry = new Mock<ITelemetryService>();
        var useCase = new ThrowingUseCase(exception);
        var command = useCase.ToAsyncRelayCommand(() => new TestRequest(), telemetry.Object, "TestActivity");

        command.Execute(null);
        await command.ExecutionTask!;

        VerifyAction(telemetry, TelemetryOutcomes.Failed);
    }

    [TestMethod]
    public async Task ToAsyncRelayCommand_ShouldTrackFailedAction_WhenUseCaseReturnsFailure()
    {
        var telemetry = new Mock<ITelemetryService>();
        var useCase = new FailedUseCase();
        var command = useCase.ToAsyncRelayCommand(() => new TestRequest(), telemetry.Object, "TestActivity");

        command.Execute(null);
        await command.ExecutionTask!;

        VerifyAction(telemetry, TelemetryOutcomes.Failed);
    }

    [TestMethod]
    public async Task ToAsyncRelayCommand_ShouldTrackCanceledAction_WhenUseCaseCancels()
    {
        var exception = new OperationCanceledException("Command canceled.");
        var telemetry = new Mock<ITelemetryService>();
        var useCase = new ThrowingUseCase(exception);
        var command = useCase.ToAsyncRelayCommand(() => new TestRequest(), telemetry.Object, "TestActivity");

        command.Execute(null);
        await command.ExecutionTask!;

        VerifyAction(telemetry, TelemetryOutcomes.Canceled);
    }

    private static void VerifyAction(
        Mock<ITelemetryService> telemetry,
        string outcome)
    {
        telemetry.Verify(
            service => service.TrackEvent(
                TelemetryEvents.UserAction,
                It.Is<IReadOnlyDictionary<string, object?>?>(
                    properties => IsAction(properties, "TestActivity", outcome))),
            Times.Once);
        telemetry.Verify(
            service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()),
            Times.Once);
    }

    private static bool IsAction(
        IReadOnlyDictionary<string, object?>? properties,
        string action,
        string outcome)
    {
        return properties is not null
            && properties.TryGetValue(TelemetryProperties.Action, out object? actualAction)
            && properties.TryGetValue(TelemetryProperties.Outcome, out object? actualOutcome)
            && Equals(actualAction, action)
            && Equals(actualOutcome, outcome);
    }

    private sealed record TestRequest;

    private sealed record TestResponse;

    private sealed class SuccessfulUseCase : IUseCase<TestRequest, TestResponse>
    {
        public Task<UseCaseResponse<TestResponse>> ExecuteAsync(
            TestRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UseCaseResponse<TestResponse>.Success(new TestResponse()));
        }
    }

    private sealed class ThrowingUseCase : IUseCase<TestRequest, TestResponse>
    {
        private readonly Exception _exception;

        public ThrowingUseCase(Exception exception)
        {
            _exception = exception;
        }

        public Task<UseCaseResponse<TestResponse>> ExecuteAsync(
            TestRequest request,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class FailedUseCase : IUseCase<TestRequest, TestResponse>
    {
        public Task<UseCaseResponse<TestResponse>> ExecuteAsync(
            TestRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UseCaseResponse<TestResponse>.Failure());
        }
    }
}

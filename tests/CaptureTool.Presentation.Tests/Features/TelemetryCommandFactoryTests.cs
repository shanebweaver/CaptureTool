using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Presentation.Shared.Commands;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class TelemetryCommandFactoryTests
{
    [TestMethod]
    public async Task Async_TracksInvocationAndSuccessfulCompletion()
    {
        var telemetry = new Mock<ITelemetryService>();
        List<(string Name, IReadOnlyDictionary<string, object?> Properties)> events = [];
        telemetry
            .Setup(service => service.TrackEvent(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (name, properties) => events.Add(
                    (name, properties ?? new Dictionary<string, object?>())));
        bool executed = false;
        var command = TelemetryCommandFactory.Async(
            "save",
            () =>
            {
                executed = true;
                return Task.CompletedTask;
            },
            telemetry.Object,
            "image_editor");

        await command.ExecuteAsync(null);

        Assert.IsTrue(executed);
        Assert.HasCount(2, events);
        Assert.AreEqual(TelemetryEvents.UiCommandInvoked, events[0].Name);
        Assert.AreEqual(TelemetryEvents.UiCommandCompleted, events[1].Name);
        Assert.AreEqual("save", events[1].Properties[TelemetryProperties.Action]);
        Assert.AreEqual("image_editor", events[1].Properties[TelemetryProperties.Surface]);
        Assert.AreEqual(
            TelemetryOutcomes.Succeeded,
            events[1].Properties[TelemetryProperties.Outcome]);
    }

    [TestMethod]
    public void Relay_TracksFailedCompletionAndPreservesException()
    {
        var telemetry = new Mock<ITelemetryService>();
        IReadOnlyDictionary<string, object?>? completedProperties = null;
        telemetry
            .Setup(service => service.TrackEvent(
                TelemetryEvents.UiCommandCompleted,
                It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, IReadOnlyDictionary<string, object?>?>(
                (_, properties) => completedProperties = properties);
        var command = TelemetryCommandFactory.Relay(
            "open_file",
            () => throw new InvalidOperationException("boom"),
            telemetry.Object,
            "app_menu");

        Assert.ThrowsExactly<InvalidOperationException>(() => command.Execute(null));

        Assert.IsNotNull(completedProperties);
        Assert.AreEqual(
            TelemetryOutcomes.Failed,
            completedProperties[TelemetryProperties.Outcome]);
    }
}

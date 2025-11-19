using CaptureTool.Services.Telemetry;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CaptureTool.ViewModels.Tests.Mocks;

internal sealed partial class MockTelemetryService : ITelemetryService
{
    public void ActivityCanceled(string activityId, string? message = null)
    {
        Debug.WriteLine($"ActivityCanceled - {activityId}: {message}");
    }

    public void ActivityCompleted(string activityId, string? message = null)
    {
        Debug.WriteLine($"ActivityCompleted - {activityId}: {message}");
    }

    public void ActivityError(string activityId, Exception exception, string? message = null, [CallerMemberName] string? callerName = null)
    {
        Debug.WriteLine($"ActivityError - {activityId}: {message}");
        Debug.WriteLine($"Caller name: {callerName}");
        Debug.WriteLine(exception.Message);
        Debug.WriteLine(exception.StackTrace);
    }

    public void ActivityInitiated(string activityId, string? message = null)
    {
        Debug.WriteLine($"ActivityInitiated - {activityId}: {message}");
    }

    public void ButtonInvoked(string buttonId, string? message)
    {
        Debug.WriteLine($"ButtonInvoked - {buttonId}: {message}");
    }
}
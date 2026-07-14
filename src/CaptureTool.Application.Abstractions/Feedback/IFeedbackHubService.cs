namespace CaptureTool.Application.Abstractions.Feedback;

public interface IFeedbackHubService
{
    Task<bool> LaunchAsync(CancellationToken cancellationToken);
}

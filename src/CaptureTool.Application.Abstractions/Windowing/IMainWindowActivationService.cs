namespace CaptureTool.Application.Abstractions.Windowing;

public interface IMainWindowActivationService
{
    Task WaitUntilActivatedAsync(CancellationToken cancellationToken = default);
    void SetActive(bool isActive);
}

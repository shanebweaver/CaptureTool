namespace CaptureTool.Application.Abstractions.Edit.External;

public interface IExternalMediaEditorLauncher
{
    Task<bool> TryOpenFileAsync(string filePath, ExternalMediaEditor editor, CancellationToken cancellationToken = default);
}

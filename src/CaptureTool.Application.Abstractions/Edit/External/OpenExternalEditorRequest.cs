namespace CaptureTool.Application.Abstractions.Edit.External;

public sealed record OpenExternalEditorRequest(string MediaPath, ExternalMediaEditor Editor);

namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>A local feature needs a model download while internet access is unavailable.</summary>
public sealed class ModelDownloadRequiredException() : Exception("Connect to the internet to download this feature's model.");

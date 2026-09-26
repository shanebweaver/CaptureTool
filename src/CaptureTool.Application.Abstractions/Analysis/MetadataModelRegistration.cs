namespace CaptureTool.Application.Abstractions.Analysis;

public enum MetadataModelBackend { WindowsLanguageModel, FoundryLocal }

/// <summary>Host registration for a configured metadata model. Only Foundry uses a catalog alias.</summary>
public sealed record MetadataModelRegistration(MetadataProcessorDescriptor Descriptor,
    MetadataModelBackend Backend, string? Alias = null);

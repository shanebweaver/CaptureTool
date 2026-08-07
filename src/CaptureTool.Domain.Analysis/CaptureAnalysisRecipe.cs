namespace CaptureTool.Domain.Analysis;

public readonly record struct AnalysisRecipeId
{
    public AnalysisRecipeId(string value)
    {
        Value = BoundedIdentifier.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString()
    {
        return Value;
    }
}

public readonly record struct AnalysisRecipeVersion
{
    public AnalysisRecipeVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A recipe version must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public bool IsEmpty => Value <= 0;

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public enum RecipeCapabilityRequirement
{
    Unknown,
    Required,
    Optional
}

public readonly record struct RecipeCapability
{
    public RecipeCapability(CapabilityDefinition capability, RecipeCapabilityRequirement requirement)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A recipe capability requires a definition.", nameof(capability));
        }

        if (!Enum.IsDefined(requirement) || requirement == RecipeCapabilityRequirement.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(requirement));
        }

        Capability = capability;
        Requirement = requirement;
    }

    public CapabilityDefinition Capability { get; }

    public RecipeCapabilityRequirement Requirement { get; }
}

public sealed class CaptureAnalysisRecipe
{
    public CaptureAnalysisRecipe(
        AnalysisRecipeId id,
        AnalysisRecipeVersion version,
        CaptureMediaKind mediaKind,
        IEnumerable<RecipeCapability> capabilities)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("A recipe requires an ID.", nameof(id));
        }

        if (version.IsEmpty)
        {
            throw new ArgumentException("A recipe requires a version.", nameof(version));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        ArgumentNullException.ThrowIfNull(capabilities);
        RecipeCapability[] copiedCapabilities = [.. capabilities];
        if (copiedCapabilities.Length == 0)
        {
            throw new ArgumentException("A recipe must request at least one capability.", nameof(capabilities));
        }

        if (copiedCapabilities.Any(capability =>
            capability.Capability.Id.IsEmpty ||
            capability.Capability.SchemaVersion.IsEmpty ||
            capability.Capability.Classification == CapabilityResultClassification.Unknown ||
            capability.Requirement == RecipeCapabilityRequirement.Unknown))
        {
            throw new ArgumentException(
                "A recipe cannot contain an empty or unknown capability requirement.",
                nameof(capabilities));
        }

        AnalysisCapabilityId? duplicateId = copiedCapabilities
            .GroupBy(capability => capability.Capability.Id)
            .Where(group => group.Count() > 1)
            .Select(group => (AnalysisCapabilityId?)group.Key)
            .FirstOrDefault();
        if (duplicateId.HasValue)
        {
            throw new ArgumentException(
                $"The recipe contains duplicate capability '{duplicateId.Value}'.",
                nameof(capabilities));
        }

        Id = id;
        Version = version;
        MediaKind = mediaKind;
        _capabilities = Array.AsReadOnly(copiedCapabilities);
    }

    private readonly IReadOnlyList<RecipeCapability> _capabilities;

    public AnalysisRecipeId Id { get; }

    public AnalysisRecipeVersion Version { get; }

    public CaptureMediaKind MediaKind { get; }

    public IReadOnlyList<RecipeCapability> Capabilities => _capabilities;

    public bool TryGetCapability(AnalysisCapabilityId id, out RecipeCapability capability)
    {
        foreach (RecipeCapability candidate in _capabilities)
        {
            if (candidate.Capability.Id == id)
            {
                capability = candidate;
                return true;
            }
        }

        capability = default;
        return false;
    }

    public bool HasSameSemanticsAs(CaptureAnalysisRecipe other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Id != other.Id || MediaKind != other.MediaKind || _capabilities.Count != other._capabilities.Count)
        {
            return false;
        }

        return _capabilities.All(capability =>
            other.TryGetCapability(capability.Capability.Id, out RecipeCapability otherCapability) &&
            capability == otherCapability);
    }
}

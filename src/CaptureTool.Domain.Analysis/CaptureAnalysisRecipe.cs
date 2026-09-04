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
    private readonly IReadOnlyList<CapabilityDefinition>? _dependencies;

    public RecipeCapability(
        CapabilityDefinition capability,
        RecipeCapabilityRequirement requirement,
        IEnumerable<CapabilityDefinition>? dependencies = null)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("A recipe capability requires a definition.", nameof(capability));
        }

        if (!Enum.IsDefined(requirement) || requirement == RecipeCapabilityRequirement.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(requirement));
        }

        CapabilityDefinition[] copiedDependencies = [.. dependencies ?? []];
        if (copiedDependencies.Any(dependency => dependency.Id.IsEmpty))
        {
            throw new ArgumentException(
                "A recipe dependency requires a capability definition.",
                nameof(dependencies));
        }

        if (copiedDependencies.Any(dependency => dependency.Id == capability.Id))
        {
            throw new ArgumentException(
                "A recipe capability cannot depend on itself.",
                nameof(dependencies));
        }

        if (copiedDependencies.GroupBy(dependency => dependency.Id).Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "A recipe capability cannot contain duplicate dependencies.",
                nameof(dependencies));
        }

        Capability = capability;
        Requirement = requirement;
        _dependencies = Array.AsReadOnly(copiedDependencies);
    }

    public CapabilityDefinition Capability { get; }

    public RecipeCapabilityRequirement Requirement { get; }

    public IReadOnlyList<CapabilityDefinition> Dependencies => _dependencies ?? [];

    public bool HasSameSemanticsAs(RecipeCapability other)
    {
        return Capability == other.Capability &&
            Requirement == other.Requirement &&
            Dependencies.Count == other.Dependencies.Count &&
            Dependencies.All(dependency => other.Dependencies.Contains(dependency));
    }
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

        var capabilitiesById = copiedCapabilities.ToDictionary(
            capability => capability.Capability.Id);
        foreach (RecipeCapability capability in copiedCapabilities)
        {
            foreach (CapabilityDefinition dependency in capability.Dependencies)
            {
                if (!capabilitiesById.TryGetValue(dependency.Id, out RecipeCapability requested) ||
                    requested.Capability != dependency)
                {
                    throw new ArgumentException(
                        $"Capability '{capability.Capability.Id}' depends on a capability " +
                        "that is not part of the recipe with the same schema.",
                        nameof(capabilities));
                }

                if (capability.Requirement == RecipeCapabilityRequirement.Required &&
                    requested.Requirement == RecipeCapabilityRequirement.Optional)
                {
                    throw new ArgumentException(
                        "A required capability cannot depend on an optional capability.",
                        nameof(capabilities));
                }
            }
        }

        EnsureAcyclic(capabilitiesById);

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
            capability.HasSameSemanticsAs(otherCapability));
    }

    public IReadOnlyList<RecipeCapability> GetExecutionOrder()
    {
        var ordered = new List<RecipeCapability>(_capabilities.Count);
        var remainingDependencies = _capabilities.ToDictionary(
            capability => capability.Capability.Id,
            capability => capability.Dependencies.Count);
        var scheduled = new HashSet<AnalysisCapabilityId>();
        while (ordered.Count < _capabilities.Count)
        {
            RecipeCapability next = _capabilities.First(capability =>
                !scheduled.Contains(capability.Capability.Id) &&
                remainingDependencies[capability.Capability.Id] == 0);
            ordered.Add(next);
            _ = scheduled.Add(next.Capability.Id);

            foreach (RecipeCapability consumer in _capabilities.Where(capability =>
                !scheduled.Contains(capability.Capability.Id) &&
                capability.Dependencies.Any(dependency => dependency.Id == next.Capability.Id)))
            {
                remainingDependencies[consumer.Capability.Id]--;
            }
        }

        return ordered.AsReadOnly();
    }

    private static void EnsureAcyclic(
        IReadOnlyDictionary<AnalysisCapabilityId, RecipeCapability> capabilities)
    {
        var visiting = new HashSet<AnalysisCapabilityId>();
        var visited = new HashSet<AnalysisCapabilityId>();

        foreach (RecipeCapability capability in capabilities.Values)
        {
            VisitForCycle(capability, capabilities, visiting, visited);
        }
    }

    private static void VisitForCycle(
        RecipeCapability capability,
        IReadOnlyDictionary<AnalysisCapabilityId, RecipeCapability> capabilities,
        ISet<AnalysisCapabilityId> visiting,
        ISet<AnalysisCapabilityId> visited)
    {
        AnalysisCapabilityId id = capability.Capability.Id;
        if (visited.Contains(id))
        {
            return;
        }

        if (!visiting.Add(id))
        {
            throw new ArgumentException("A recipe capability dependency graph cannot contain a cycle.");
        }

        foreach (CapabilityDefinition dependency in capability.Dependencies)
        {
            VisitForCycle(capabilities[dependency.Id], capabilities, visiting, visited);
        }

        _ = visiting.Remove(id);
        _ = visited.Add(id);
    }
}

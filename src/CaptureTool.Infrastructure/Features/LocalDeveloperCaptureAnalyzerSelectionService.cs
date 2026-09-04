#if DEBUG
using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.Definitions;
using CaptureTool.Domain.Analysis;
using System.Globalization;
using System.Text;

namespace CaptureTool.Infrastructure.Features;

internal sealed class LocalDeveloperCaptureAnalyzerSelectionService :
    ICaptureAnalyzerSelectionService
{
    private const string FormatVersion = "v1";
    private const int MaximumDocumentLength = 16 * 1024;
    private const int SelectedAnalyzerPreference = 10_000;
    private static readonly IStringSettingDefinition SelectionSetting = new StringSettingDefinition(
        "Developer_CaptureAnalysis_AnalyzerSelections",
        string.Empty);
    private static readonly CapabilityDefinition[] KnownCapabilities =
    [
        AnalysisCapabilities.MediaPropertiesV1,
        AnalysisCapabilities.OcrDocumentV1,
        AnalysisCapabilities.ImageDescriptionV1,
        AnalysisCapabilities.SpeechTranscriptV1,
        AnalysisCapabilities.VideoOcrTrackV1,
        AnalysisCapabilities.VideoDescriptionTrackV1,
    ];

    private readonly ISettingsService _settings;
    private readonly ICaptureAnalyzerCatalog _catalog;

    public LocalDeveloperCaptureAnalyzerSelectionService(
        ISettingsService settings,
        ICaptureAnalyzerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(catalog);
        _settings = settings;
        _catalog = catalog;
    }

    public long Revision => ReadSnapshot().Revision;

    public CaptureAnalyzerSelection GetSelection(CapabilityDefinition capability)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("An analyzer selection requires a capability.", nameof(capability));
        }

        SelectionSnapshot snapshot = ReadSnapshot();
        if (!snapshot.Selections.TryGetValue(capability, out CaptureAnalyzerSelection? selection))
        {
            return CaptureAnalyzerSelection.Automatic(capability);
        }

        if (selection.Target != null && !TargetExists(selection))
        {
            return CaptureAnalyzerSelection.Automatic(capability);
        }

        return selection;
    }

    public int GetPreference(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        CaptureAnalyzerSelection selection = GetSelection(descriptor.Capability);
        return (selection.Mode is CaptureAnalyzerSelectionMode.Prefer or
            CaptureAnalyzerSelectionMode.Force) && Matches(selection.Target, descriptor.Identity)
                ? SelectedAnalyzerPreference
                : 0;
    }

    public bool IsAllowed(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        CaptureAnalyzerSelection selection = GetSelection(descriptor.Capability);
        return selection.Mode switch
        {
            CaptureAnalyzerSelectionMode.Off => false,
            CaptureAnalyzerSelectionMode.Force => Matches(selection.Target, descriptor.Identity),
            _ => true,
        };
    }

    public bool? GetFeatureEnabledOverride(AnalyzerIdentity analyzer)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        CaptureAnalyzerDescriptor? descriptor = _catalog.Analyzers
            .Select(candidate => candidate.Descriptor)
            .FirstOrDefault(candidate => Matches(
                new CaptureAnalyzerSelectionTarget(
                    analyzer.ProviderId,
                    analyzer.AnalyzerId),
                candidate.Identity));
        if (descriptor == null)
        {
            return null;
        }

        CaptureAnalyzerSelection selection = GetSelection(descriptor.Capability);
        return selection.Mode switch
        {
            CaptureAnalyzerSelectionMode.Off => false,
            CaptureAnalyzerSelectionMode.Force => Matches(selection.Target, analyzer),
            CaptureAnalyzerSelectionMode.Prefer when Matches(selection.Target, analyzer) => true,
            _ => null,
        };
    }

    public async ValueTask<CaptureAnalyzerSelectionSaveResult> SaveAsync(
        IEnumerable<CaptureAnalyzerSelection> selections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selections);
        cancellationToken.ThrowIfCancellationRequested();

        CaptureAnalyzerSelection[] copied = [.. selections];
        if (copied.Any(selection => selection == null) ||
            copied.GroupBy(selection => selection.Capability).Any(group => group.Count() > 1) ||
            copied.Any(selection => selection.Target != null && !TargetExists(selection)))
        {
            return new(CaptureAnalyzerSelectionSaveStatus.InvalidSelection);
        }

        SelectionSnapshot current = ReadSnapshot();
        Dictionary<CapabilityDefinition, CaptureAnalyzerSelection> nextSelections = copied
            .Where(selection => selection.Mode != CaptureAnalyzerSelectionMode.Automatic)
            .ToDictionary(selection => selection.Capability);
        if (SelectionsEqual(current.Selections, nextSelections))
        {
            return new(CaptureAnalyzerSelectionSaveStatus.Unchanged);
        }

        if (current.Revision == long.MaxValue)
        {
            return new(CaptureAnalyzerSelectionSaveStatus.Unavailable);
        }

        string serialized = Serialize(new SelectionSnapshot(current.Revision + 1, nextSelections));
        SettingsMutationResult result;
        try
        {
            result = await _settings.TrySetAndSaveAsync(
                SelectionSetting,
                serialized,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return new(CaptureAnalyzerSelectionSaveStatus.Unavailable);
        }

        return new(result.Status switch
        {
            SettingsMutationStatus.Saved => CaptureAnalyzerSelectionSaveStatus.Saved,
            SettingsMutationStatus.PersistenceFailed =>
                CaptureAnalyzerSelectionSaveStatus.PersistenceFailed,
            _ => CaptureAnalyzerSelectionSaveStatus.Unavailable,
        });
    }

    private SelectionSnapshot ReadSnapshot()
    {
        try
        {
            return Parse(_settings.Get(SelectionSetting));
        }
        catch (InvalidOperationException)
        {
            return SelectionSnapshot.Empty;
        }
    }

    private bool TargetExists(CaptureAnalyzerSelection selection) =>
        selection.Target != null && _catalog.Analyzers.Any(analyzer =>
            analyzer.Descriptor.Capability == selection.Capability &&
            Matches(selection.Target, analyzer.Descriptor.Identity));

    private static bool Matches(
        CaptureAnalyzerSelectionTarget? target,
        AnalyzerIdentity identity) =>
        target != null &&
        string.Equals(target.ProviderId, identity.ProviderId, StringComparison.Ordinal) &&
        string.Equals(target.AnalyzerId, identity.AnalyzerId, StringComparison.Ordinal);

    private static bool SelectionsEqual(
        IReadOnlyDictionary<CapabilityDefinition, CaptureAnalyzerSelection> left,
        IReadOnlyDictionary<CapabilityDefinition, CaptureAnalyzerSelection> right) =>
        left.Count == right.Count && left.All(pair =>
            right.TryGetValue(pair.Key, out CaptureAnalyzerSelection? value) && pair.Value == value);

    private static SelectionSnapshot Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumDocumentLength)
        {
            return SelectionSnapshot.Empty;
        }

        string[] lines = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || !string.Equals(lines[0], FormatVersion, StringComparison.Ordinal) ||
            !long.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out long revision) ||
            revision < 0)
        {
            return SelectionSnapshot.Empty;
        }

        var selections = new Dictionary<CapabilityDefinition, CaptureAnalyzerSelection>();
        try
        {
            foreach (string line in lines.Skip(2))
            {
                string[] fields = line.Split('\t');
                if (fields.Length != 5 ||
                    !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int schema) ||
                    !Enum.TryParse(fields[2], ignoreCase: false, out CaptureAnalyzerSelectionMode mode))
                {
                    return SelectionSnapshot.Empty;
                }

                CapabilityDefinition capability = KnownCapabilities.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id.Value, fields[0], StringComparison.Ordinal) &&
                    candidate.SchemaVersion.Value == schema);
                if (capability.Id.IsEmpty || mode is CaptureAnalyzerSelectionMode.Unknown or
                    CaptureAnalyzerSelectionMode.Automatic)
                {
                    return SelectionSnapshot.Empty;
                }

                CaptureAnalyzerSelectionTarget? target = mode is CaptureAnalyzerSelectionMode.Prefer or
                    CaptureAnalyzerSelectionMode.Force
                        ? new CaptureAnalyzerSelectionTarget(fields[3], fields[4])
                        : null;
                var selection = new CaptureAnalyzerSelection(capability, mode, target);
                if (!selections.TryAdd(capability, selection))
                {
                    return SelectionSnapshot.Empty;
                }
            }
        }
        catch (ArgumentException)
        {
            return SelectionSnapshot.Empty;
        }

        return new(revision, selections);
    }

    private static string Serialize(SelectionSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.AppendLine(FormatVersion);
        builder.AppendLine(snapshot.Revision.ToString(CultureInfo.InvariantCulture));
        foreach (CaptureAnalyzerSelection selection in snapshot.Selections.Values
            .OrderBy(selection => selection.Capability.Id.Value, StringComparer.Ordinal))
        {
            builder.Append(selection.Capability.Id.Value).Append('\t')
                .Append(selection.Capability.SchemaVersion.Value.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(selection.Mode).Append('\t')
                .Append(selection.Target?.ProviderId ?? string.Empty).Append('\t')
                .Append(selection.Target?.AnalyzerId ?? string.Empty).AppendLine();
        }

        return builder.ToString();
    }

    private sealed record SelectionSnapshot(
        long Revision,
        IReadOnlyDictionary<CapabilityDefinition, CaptureAnalyzerSelection> Selections)
    {
        public static SelectionSnapshot Empty { get; } = new(
            0,
            new Dictionary<CapabilityDefinition, CaptureAnalyzerSelection>());
    }
}
#endif

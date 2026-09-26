namespace CaptureTool.Domain.Analysis;

/// <summary>Exact inputs consulted by one processor, scoped to the containing capture revision.</summary>
public sealed class AnalysisDerivation
{
    public IReadOnlyList<AnalysisInputReference> Inputs { get; }

    public AnalysisDerivation(IEnumerable<AnalysisInputReference> inputs)
    {
        Inputs = AnalysisGuard.Freeze(inputs);
        if (!Inputs.Any(input => input.ResultId != null) ||
            Inputs.Select(input => input.Capability).Distinct().Count() != Inputs.Count ||
            Inputs.Where(input => input.ResultId != null).Select(input => input.ResultId).Distinct().Count() !=
                Inputs.Count(input => input.ResultId != null))
        {
            throw new ArgumentException("Specify distinct capabilities and identities with at least one present input.", nameof(inputs));
        }
    }

    public bool Matches(IReadOnlyList<AnalysisResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Inputs.All(input => results.SingleOrDefault(result => result.Payload.Capability == input.Capability)?.ResultId == input.ResultId);
    }
}

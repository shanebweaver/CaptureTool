using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>The benefits a device can offer, before consent or model preparation.</summary>
public sealed record AnalysisFeatureBenefits(bool Text, bool Summary, bool Names)
{
    public bool Any => Text || Summary || Names;
    public static async Task<AnalysisFeatureBenefits> ReadAsync(IAnalysisFeatureAvailability availability,
        CancellationToken cancellationToken = default)
    {
        var text = availability.IsSupportedAsync(AnalysisMediaKind.Image, AnalysisCapability.TextRecognition, cancellationToken);
        var summary = AnyMediaAsync(AnalysisCapability.CaptureSynopsis);
        var names = AnyMediaAsync(AnalysisCapability.CaptureName);
        await Task.WhenAll(text, summary, names).ConfigureAwait(false);
        return new(await text, await summary, await names);

        async Task<bool> AnyMediaAsync(AnalysisCapability capability) =>
            (await Task.WhenAll(new[] { AnalysisMediaKind.Image, AnalysisMediaKind.Audio, AnalysisMediaKind.Video }
                .Select(kind => availability.IsSupportedAsync(kind, capability, cancellationToken))).ConfigureAwait(false)).Any(value => value);
    }
}

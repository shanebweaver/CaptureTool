using CaptureTool.Application.Abstractions.Analysis;

namespace CaptureTool.Application.Analysis;

/// <summary>Reports one provider attempt and fences its late callbacks when disposed.</summary>
internal sealed class AnalysisProgressScope : IProgress<AnalysisProgress>, IDisposable
{
    private readonly Action<AnalysisProgress> _report;
    private readonly Action _close;

    public AnalysisProgressScope(Action<AnalysisProgress> report, Action close)
    {
        _report = report;
        _close = close;
    }

    public void Report(AnalysisProgress value) => _report(value);
    public void Dispose() => _close();
}

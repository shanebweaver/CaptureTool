namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>Controls reusable provider resources after inference stops.</summary>
public interface IAnalysisResources
{
    /// <summary>Maximum idle retention after the last requested work, or zero for immediate release.</summary>
    TimeSpan IdleRetention => TimeSpan.Zero;
    /// <summary>Memory pressure bypasses idle retention; active inference keeps ownership until it stops.</summary>
    bool IsUnderMemoryPressure => false;
    Task ReleaseAsync();
}

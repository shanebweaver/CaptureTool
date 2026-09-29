using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed record CaptureMemoryPolicy(bool ScanningEnabled, bool ConsentGranted, Guid Revision, long EnableBoundary,
    bool? ScanningPreference = null)
{
    // The legacy automatic-scanning fields remain readable for stored-policy compatibility.
    // Explicit actions require consent, independently of a former automation preference.
    public bool IsAllowed => ConsentGranted;
    public static CaptureMemoryPolicy Disabled() => new(false, false, Guid.NewGuid(), 0);
}

public interface ICaptureMemoryPolicyStore
{
    Task<CaptureMemoryPolicy?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CaptureMemoryPolicy policy, CancellationToken cancellationToken);
}

public enum CaptureMemoryPrompt { Consent, DeleteMetadata }
public interface ICaptureMemoryPrompts
{
    Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken cancellationToken);
}

public sealed record AnalysisStorageStatus(bool? HasData, bool IsAvailable, bool CleanupPending = false);
public sealed record CaptureMemoryState(CaptureMemoryPolicy Policy, bool PolicyAvailable,
    AnalysisStorageStatus Storage, AnalysisActivitySnapshot Activity, bool IsScheduling = false, string? FailureCode = null,
    bool IsDeleting = false, bool ConsentAvailable = true)
{
    public bool CanScan => PolicyAvailable && Policy.IsAllowed && !IsScheduling && !IsDeleting;
    public bool CanDelete => Storage.HasData == true && !IsDeleting;
    public bool IsLoading => PolicyAvailable && Policy.IsAllowed && Activity.Activity is AnalysisActivity.Preparing or AnalysisActivity.Analyzing;
}

/// <summary>Application-owned consent, intake, commands, and runner lifetime. Views only observe this state.</summary>
public interface ICaptureMemoryService
{
    CaptureMemoryState State { get; }
    event Action? StateChanged;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    /// <summary>Records capture identity only; never schedules analysis.</summary>
    Task RegisterCaptureAsync(CaptureAsset asset, CancellationToken cancellationToken = default);
    Task SetPreferredPathAsync(string sourcePath, string preferredPath, CancellationToken cancellationToken = default);
    Task SetConsentAsync(bool granted, CancellationToken cancellationToken = default);
    Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default);
    /// <summary>Requests one action and its prerequisites. Capture/open/tab lifecycle events never call this.</summary>
    Task AnalyzeAsync(string path, AnalysisCapability capability, CancellationToken cancellationToken = default);
    /// <summary>Requests OCR and QR detection together for visual media, reusing either completed scan.</summary>
    Task ScanTextAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteMetadataAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}

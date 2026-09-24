using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Analysis;

public sealed record CaptureMemoryPolicy(bool ScanningEnabled, bool ConsentGranted, Guid Revision, long EnableBoundary)
{
    public bool IsAllowed => ScanningEnabled && ConsentGranted;
    public static CaptureMemoryPolicy Disabled() => new(false, false, Guid.NewGuid(), 0);
}

public interface ICaptureMemoryPolicyStore
{
    Task<CaptureMemoryPolicy?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CaptureMemoryPolicy policy, CancellationToken cancellationToken);
}

public enum CaptureMemoryPrompt { Consent, ScanExisting, DeleteMetadata }
public interface ICaptureMemoryPrompts
{
    Task<bool> ConfirmAsync(CaptureMemoryPrompt prompt, CancellationToken cancellationToken);
}

public sealed record AnalysisStorageStatus(bool? HasData, bool IsAvailable, bool CleanupPending = false);
public sealed record CaptureMemoryState(CaptureMemoryPolicy Policy, bool PolicyAvailable,
    AnalysisStorageStatus Storage, AnalysisActivitySnapshot Activity, bool IsScheduling = false, string? FailureCode = null,
    bool IsDeleting = false)
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
    Guid? CaptureAuthorization { get; }
    Task RegisterCaptureAsync(CaptureAsset asset, Guid? authorization, CancellationToken cancellationToken = default);
    Task SetPreferredPathAsync(string sourcePath, string preferredPath, CancellationToken cancellationToken = default);
    Task SetScanningAsync(bool enabled, CancellationToken cancellationToken = default);
    Task SetConsentAsync(bool granted, CancellationToken cancellationToken = default);
    Task ScanExistingAsync(CancellationToken cancellationToken = default);
    Task DeleteMetadataAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}

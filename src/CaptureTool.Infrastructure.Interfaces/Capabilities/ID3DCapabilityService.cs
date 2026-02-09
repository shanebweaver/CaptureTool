namespace CaptureTool.Infrastructure.Interfaces.Capabilities;

/// <summary>
/// Service for checking Direct3D device capabilities at runtime.
/// </summary>
public interface ID3DCapabilityService
{
    /// <summary>
    /// Checks if the system supports the required Direct3D 11 features for capture operations.
    /// </summary>
    /// <returns>
    /// A result indicating whether D3D requirements are met, or an error with details.
    /// </returns>
    D3DCapabilityCheckResult CheckD3DCapabilities();

    /// <summary>
    /// Gets a user-friendly error message when D3D capabilities are not met.
    /// </summary>
    /// <returns>A localized error message with guidance for the user.</returns>
    string GetUnsupportedDeviceMessage();
}

/// <summary>
/// Result of a D3D capability check.
/// </summary>
public sealed class D3DCapabilityCheckResult
{
    /// <summary>
    /// Gets whether the system meets all D3D requirements.
    /// </summary>
    public bool IsSupported { get; init; }

    /// <summary>
    /// Gets the error message if the system does not meet requirements.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the HRESULT error code if available.
    /// </summary>
    public int? HResult { get; init; }

    /// <summary>
    /// Gets details about which specific requirement failed.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static D3DCapabilityCheckResult Success() => new()
    {
        IsSupported = true
    };

    /// <summary>
    /// Creates a failed result with error details.
    /// </summary>
    public static D3DCapabilityCheckResult Failure(string failureReason, string errorMessage, int? hresult = null) => new()
    {
        IsSupported = false,
        FailureReason = failureReason,
        ErrorMessage = errorMessage,
        HResult = hresult
    };
}

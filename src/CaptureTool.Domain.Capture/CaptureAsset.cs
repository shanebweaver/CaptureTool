using CaptureTool.Domain;

namespace CaptureTool.Domain.Capture;

public sealed class CaptureAsset
{
    public CaptureAsset(
        CaptureId id,
        CaptureFileType mediaType,
        string retainedSourcePath,
        CaptureSourceOwnership sourceOwnership,
        DateTimeOffset capturedAtUtc,
        string? preferredOpenPath = null)
        : this(
            id,
            mediaType,
            retainedSourcePath,
            sourceOwnership,
            preferredOpenPath,
            capturedAtUtc,
            CaptureAssetLifecycleState.Active,
            1)
    {
    }

    public CaptureAsset(
        CaptureId id,
        CaptureFileType mediaType,
        string retainedSourcePath,
        CaptureSourceOwnership sourceOwnership,
        string? preferredOpenPath,
        DateTimeOffset capturedAtUtc,
        CaptureAssetLifecycleState lifecycleState,
        long lifecycleRevision)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("A capture asset requires a non-empty ID.", nameof(id));
        }

        if (!Enum.IsDefined(mediaType) || mediaType == CaptureFileType.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaType), "A capture asset requires a known media type.");
        }

        if (!Enum.IsDefined(sourceOwnership) || sourceOwnership == CaptureSourceOwnership.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOwnership), "A capture asset requires known source ownership.");
        }

        if (!Enum.IsDefined(lifecycleState))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleState));
        }

        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A captured timestamp must be expressed in UTC.", nameof(capturedAtUtc));
        }

        if (lifecycleRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycleRevision), "A lifecycle revision must be positive.");
        }

        Id = id;
        MediaType = mediaType;
        RetainedSourcePath = ValidateAbsolutePath(retainedSourcePath, nameof(retainedSourcePath));
        SourceOwnership = sourceOwnership;
        PreferredOpenPath = ValidateOptionalAbsolutePath(preferredOpenPath, nameof(preferredOpenPath));
        CapturedAtUtc = capturedAtUtc;
        LifecycleState = lifecycleState;
        LifecycleRevision = lifecycleRevision;
    }

    public CaptureId Id { get; }

    public CaptureFileType MediaType { get; }

    public string RetainedSourcePath { get; }

    public CaptureSourceOwnership SourceOwnership { get; }

    public string? PreferredOpenPath { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public CaptureAssetLifecycleState LifecycleState { get; }

    public long LifecycleRevision { get; }

    public static CaptureAsset Create(
        CaptureFileType mediaType,
        string retainedSourcePath,
        CaptureSourceOwnership sourceOwnership,
        DateTimeOffset capturedAtUtc,
        string? preferredOpenPath = null)
    {
        return new(
            CaptureId.New(),
            mediaType,
            retainedSourcePath,
            sourceOwnership,
            capturedAtUtc,
            preferredOpenPath);
    }

    public CaptureAsset ChangeSource(string retainedSourcePath, CaptureSourceOwnership sourceOwnership)
    {
        EnsureActive();

        string validatedPath = ValidateAbsolutePath(retainedSourcePath, nameof(retainedSourcePath));
        if (!Enum.IsDefined(sourceOwnership) || sourceOwnership == CaptureSourceOwnership.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOwnership), "A capture asset requires known source ownership.");
        }

        if (PathsEqual(RetainedSourcePath, validatedPath) && SourceOwnership == sourceOwnership)
        {
            return this;
        }

        return new(
            Id,
            MediaType,
            validatedPath,
            sourceOwnership,
            PreferredOpenPath,
            CapturedAtUtc,
            LifecycleState,
            GetNextLifecycleRevision());
    }

    public CaptureAsset ChangePreferredOpenPath(string? preferredOpenPath)
    {
        EnsureActive();

        string? validatedPath = ValidateOptionalAbsolutePath(preferredOpenPath, nameof(preferredOpenPath));
        if (PathsEqual(PreferredOpenPath, validatedPath))
        {
            return this;
        }

        return new(
            Id,
            MediaType,
            RetainedSourcePath,
            SourceOwnership,
            validatedPath,
            CapturedAtUtc,
            LifecycleState,
            GetNextLifecycleRevision());
    }

    public CaptureAsset MarkDeleted()
    {
        if (LifecycleState == CaptureAssetLifecycleState.Deleted)
        {
            return this;
        }

        return new(
            Id,
            MediaType,
            RetainedSourcePath,
            SourceOwnership,
            PreferredOpenPath,
            CapturedAtUtc,
            CaptureAssetLifecycleState.Deleted,
            GetNextLifecycleRevision());
    }

    private static string ValidateAbsolutePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A capture source path must be absolute.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private static string? ValidateOptionalAbsolutePath(string? path, string parameterName)
    {
        if (path == null)
        {
            return null;
        }

        return ValidateAbsolutePath(path, parameterName);
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureActive()
    {
        if (LifecycleState == CaptureAssetLifecycleState.Deleted)
        {
            throw new InvalidOperationException("A deleted capture asset cannot be changed.");
        }
    }

    private long GetNextLifecycleRevision()
    {
        return checked(LifecycleRevision + 1);
    }
}

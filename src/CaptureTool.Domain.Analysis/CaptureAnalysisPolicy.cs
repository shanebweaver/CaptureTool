namespace CaptureTool.Domain.Analysis;

public sealed class CaptureAnalysisPolicy
{
    public CaptureAnalysisPolicy(
        CaptureAnalysisConsentState consentState,
        long policyRevision,
        long controlGeneration,
        CaptureAnalysisAuthorizationScope? authorizationScope,
        bool isFutureCaptureAdmissionEnabled,
        long futureCaptureSequenceWatermark,
        CaptureAnalysisBackfillState backfillState,
        long backfillUpperSequence,
        long backfillCheckpoint)
    {
        if (!Enum.IsDefined(consentState))
        {
            throw new ArgumentOutOfRangeException(nameof(consentState));
        }

        EnsureNonNegative(policyRevision, nameof(policyRevision));
        EnsureNonNegative(controlGeneration, nameof(controlGeneration));
        EnsureNonNegative(futureCaptureSequenceWatermark, nameof(futureCaptureSequenceWatermark));
        EnsureNonNegative(backfillUpperSequence, nameof(backfillUpperSequence));
        EnsureNonNegative(backfillCheckpoint, nameof(backfillCheckpoint));

        if (!Enum.IsDefined(backfillState) || backfillState == CaptureAnalysisBackfillState.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(backfillState));
        }

        bool isGranted = consentState == CaptureAnalysisConsentState.Granted;
        if (isGranted != (authorizationScope != null))
        {
            throw new ArgumentException(
                "Only granted Capture Analysis consent can retain an authorization scope.",
                nameof(authorizationScope));
        }

        if (isGranted && policyRevision == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(policyRevision),
                "Granted Capture Analysis consent requires a positive policy revision.");
        }

        if (!isGranted && isFutureCaptureAdmissionEnabled)
        {
            throw new ArgumentException(
                "Future-capture admission requires granted Capture Analysis consent.",
                nameof(isFutureCaptureAdmissionEnabled));
        }

        if (backfillCheckpoint > backfillUpperSequence)
        {
            throw new ArgumentOutOfRangeException(
                nameof(backfillCheckpoint),
                "A backfill checkpoint cannot exceed its authorized upper sequence.");
        }

        if (backfillState == CaptureAnalysisBackfillState.NotAuthorized &&
            (backfillUpperSequence != 0 || backfillCheckpoint != 0))
        {
            throw new ArgumentException(
                "An unauthorized backfill cannot retain a sequence scope or checkpoint.");
        }

        if (backfillState != CaptureAnalysisBackfillState.NotAuthorized && !isGranted)
        {
            throw new ArgumentException(
                "Existing-capture backfill requires granted Capture Analysis consent.",
                nameof(backfillState));
        }

        if (backfillState == CaptureAnalysisBackfillState.Authorized && backfillCheckpoint != 0)
        {
            throw new ArgumentException(
                "An authorized backfill has not started and must have a zero checkpoint.",
                nameof(backfillCheckpoint));
        }

        if (backfillState == CaptureAnalysisBackfillState.Completed &&
            backfillCheckpoint != backfillUpperSequence)
        {
            throw new ArgumentException(
                "A completed backfill checkpoint must equal its authorized upper sequence.",
                nameof(backfillCheckpoint));
        }

        ConsentState = consentState;
        PolicyRevision = policyRevision;
        ControlGeneration = controlGeneration;
        AuthorizationScope = authorizationScope;
        IsFutureCaptureAdmissionEnabled = isFutureCaptureAdmissionEnabled;
        FutureCaptureSequenceWatermark = futureCaptureSequenceWatermark;
        BackfillState = backfillState;
        BackfillUpperSequence = backfillUpperSequence;
        BackfillCheckpoint = backfillCheckpoint;
    }

    public static CaptureAnalysisPolicy Unknown { get; } = new(
        CaptureAnalysisConsentState.Unknown,
        0,
        0,
        null,
        false,
        0,
        CaptureAnalysisBackfillState.NotAuthorized,
        0,
        0);

    public CaptureAnalysisConsentState ConsentState { get; }

    // Advances only for a newly reviewed authorization scope. Admission-only changes deliberately
    // leave this fence stable so already-enrolled work may finish.
    public long PolicyRevision { get; }

    // Advances for global destructive changes so late work cannot recreate erased metadata.
    public long ControlGeneration { get; }

    public CaptureAnalysisAuthorizationScope? AuthorizationScope { get; }

    public AnalysisPurpose? AuthorizedPurpose => AuthorizationScope?.Purpose;

    public AnalysisProcessingPolicy? ProcessingPolicy => AuthorizationScope?.ProcessingPolicy;

    public bool IsFutureCaptureAdmissionEnabled { get; }

    public long FutureCaptureSequenceWatermark { get; }

    public CaptureAnalysisBackfillState BackfillState { get; }

    public long BackfillUpperSequence { get; }

    public long BackfillCheckpoint { get; }

    public bool IsProcessingAuthorized =>
        ConsentState == CaptureAnalysisConsentState.Granted &&
        AuthorizationScope != null;

    public CaptureAnalysisPolicy GrantFutureCaptures(
        CaptureAnalysisAuthorizationScope reviewedScope,
        long currentSequence)
    {
        ArgumentNullException.ThrowIfNull(reviewedScope);

        EnsureCurrentSequence(currentSequence);

        return new(
            CaptureAnalysisConsentState.Granted,
            Increment(PolicyRevision, nameof(PolicyRevision)),
            ControlGeneration,
            reviewedScope,
            true,
            currentSequence,
            CaptureAnalysisBackfillState.NotAuthorized,
            0,
            0);
    }

    public CaptureAnalysisPolicy ResumeFutureCaptures(long currentSequence)
    {
        EnsureCurrentSequence(currentSequence);
        if (!IsProcessingAuthorized)
        {
            throw new InvalidOperationException(
                "Future Capture Analysis can resume only under an existing grant.");
        }

        if (IsFutureCaptureAdmissionEnabled)
        {
            return this;
        }

        return new(
            ConsentState,
            PolicyRevision,
            ControlGeneration,
            AuthorizationScope,
            true,
            currentSequence,
            BackfillState,
            BackfillUpperSequence,
            BackfillCheckpoint);
    }

    public CaptureAnalysisPolicy StopFutureCaptures(long currentSequence)
    {
        EnsureCurrentSequence(currentSequence);
        if (!IsProcessingAuthorized || !IsFutureCaptureAdmissionEnabled)
        {
            return this;
        }

        return new(
            ConsentState,
            PolicyRevision,
            ControlGeneration,
            AuthorizationScope,
            false,
            currentSequence,
            BackfillState,
            BackfillUpperSequence,
            BackfillCheckpoint);
    }

    public CaptureAnalysisPolicy AuthorizeExistingCaptureBackfill(long currentSequence)
    {
        EnsureCurrentSequence(currentSequence);
        if (!IsProcessingAuthorized)
        {
            throw new InvalidOperationException(
                "Existing-capture backfill requires granted Capture Analysis consent.");
        }

        if (BackfillState == CaptureAnalysisBackfillState.Authorized &&
            BackfillUpperSequence == currentSequence)
        {
            return this;
        }

        return new(
            ConsentState,
            PolicyRevision,
            ControlGeneration,
            AuthorizationScope,
            IsFutureCaptureAdmissionEnabled,
            FutureCaptureSequenceWatermark,
            CaptureAnalysisBackfillState.Authorized,
            currentSequence,
            0);
    }

    public CaptureAnalysisPolicy Revoke()
    {
        return new(
            CaptureAnalysisConsentState.Denied,
            Increment(PolicyRevision, nameof(PolicyRevision)),
            Increment(ControlGeneration, nameof(ControlGeneration)),
            null,
            false,
            FutureCaptureSequenceWatermark,
            CaptureAnalysisBackfillState.NotAuthorized,
            0,
            0);
    }

    public CaptureAnalysisPolicy ClearMemory(long currentSequence)
    {
        EnsureCurrentSequence(currentSequence);
        if (!IsProcessingAuthorized)
        {
            throw new InvalidOperationException(
                "Capture Memory can be cleared only while Capture Analysis consent is granted.");
        }

        return new(
            ConsentState,
            PolicyRevision,
            Increment(ControlGeneration, nameof(ControlGeneration)),
            AuthorizationScope,
            IsFutureCaptureAdmissionEnabled,
            currentSequence,
            CaptureAnalysisBackfillState.NotAuthorized,
            0,
            0);
    }

    public bool IsFutureCaptureEligible(long sequence)
    {
        return sequence > 0 &&
            IsProcessingAuthorized &&
            IsFutureCaptureAdmissionEnabled &&
            sequence > FutureCaptureSequenceWatermark;
    }

    public bool IsExistingCaptureBackfillEligible(long sequence)
    {
        return sequence > 0 &&
            IsProcessingAuthorized &&
            (BackfillState is CaptureAnalysisBackfillState.Authorized or
                CaptureAnalysisBackfillState.InProgress) &&
            sequence > BackfillCheckpoint &&
            sequence <= BackfillUpperSequence;
    }

    private void EnsureCurrentSequence(long currentSequence)
    {
        EnsureNonNegative(currentSequence, nameof(currentSequence));
        if (currentSequence < FutureCaptureSequenceWatermark)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentSequence),
                "The current Capture Asset sequence cannot move behind the durable watermark.");
        }
    }

    private static long Increment(long value, string parameterName)
    {
        try
        {
            return checked(value + 1);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"The {parameterName} fence cannot advance beyond its supported range.",
                exception);
        }
    }

    private static void EnsureNonNegative(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "A revision or sequence cannot be negative.");
        }
    }
}

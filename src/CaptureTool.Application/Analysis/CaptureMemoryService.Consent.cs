using CaptureTool.Application.Abstractions.Analysis;

namespace CaptureTool.Application.Analysis;

internal sealed partial class CaptureMemoryService
{
    public Task SetConsentAsync(bool granted, CancellationToken cancellationToken = default)
        => granted ? GrantConsentAsync(cancellationToken) : UpdateConsentAsync(false, cancellationToken);

    private async Task GrantConsentAsync(CancellationToken ct)
    {
        long attempt = Interlocked.Read(ref _consentAttempt);
        await _consentRequests.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_authorization.IsConsentAllowed || attempt != Interlocked.Read(ref _consentAttempt))
            {
                return;
            }

            await UpdateConsentAsync(true, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _consentAttempt);
        }
        finally
        {
            _consentRequests.Release();
        }
    }

    private Task UpdateConsentAsync(bool granted, CancellationToken cancellationToken)
    {
        if (granted && _authorization.IsConsentAllowed)
        {
            return Task.CompletedTask;
        }

        long epoch = Interlocked.Increment(ref _epoch);
        if (!granted)
        {
            _authorization.Block(revokeConsent: true);
            Publish();
        }
        return RunSafelyAsync(async ct =>
        {
            if (!_authorization.IsLoaded)
            {
                SetFailure("policy-unavailable");
                return;
            }
            if (granted && !await _prompts.ConfirmAsync(CaptureMemoryPrompt.Consent, ct).ConfigureAwait(false))
            {
                return;
            }

            bool applied = false;
            await RunCommandAsync(async () =>
            {
                if (!CanApplyPolicy(epoch, granted))
                {
                    return;
                }

                await _authorization.SaveAsync(new(granted, Guid.NewGuid()), ct, grantConsent: granted).ConfigureAwait(false);
                if (granted)
                {
                    _lastGrantedEpoch = epoch;
                }

                applied = true;
                await RefreshStatusAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
            if (!applied || !IsCurrentEpoch(epoch))
            {
                return;
            }

            if (!granted)
            {
                await OfferDeletionAsync(epoch, ct).ConfigureAwait(false);
            }
        }, cancellationToken);
    }

    public async Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (!_authorization.IsConsentAllowed)
        {
            await SetConsentAsync(true, cancellationToken).ConfigureAwait(false);
        }

        return !cancellationToken.IsCancellationRequested && _authorization.IsConsentAllowed;
    }

    private async Task InitializePolicyAsync(CancellationToken ct)
    {
        try
        {
            await _authorization.InitializeAsync(ct).ConfigureAwait(false);
        }
        catch (CaptureMemoryPolicyException)
        {
            // Consent failure blocks scanning, but does not hide the user's ability to delete data.
            _storage = await _store.GetStorageStatusAsync(ct).ConfigureAwait(false);
            throw;
        }
    }
}

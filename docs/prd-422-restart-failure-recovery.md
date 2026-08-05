# PRD: Recover safely from rejected application restart

- Issue: [#422](https://github.com/shanebweaver/CaptureTool/issues/422)
- Finding: `ARCH-17`
- Severity: Medium
- Status: Implemented
- Affected feature: `APP-08`

## Summary

Application restart must be treated as an attempted platform transition rather than a shutdown that is assumed to succeed. The running process must remain usable when Windows rejects the request, the rejection must propagate through the application use case, and the error page must present a clear retry-or-exit recovery path.

The static Windows restart API will be isolated behind an adapter so every documented `AppRestartFailureReason` can be exercised without terminating a test process.

## Problem

`WindowsShutdownHandler.TryRestart` currently calls `Teardown` before `AppInstance.Restart`. Teardown permanently marks the process as shutting down and cancels the global application token. The Windows API returns only when the restart request fails, so every returned failure leaves the original process alive but irreversibly torn down.

The restart application use cases also ignore `TryRestart`'s Boolean result and return a successful response. On the error page, the command therefore produces no visible outcome and can become disabled because the handler is already marked as shutting down.

## Goals

1. Preserve the running process and global cancellation state whenever Windows rejects restart.
2. Exercise every Windows restart failure reason through a test adapter.
3. Return the handler result from both restart use cases instead of reporting unconditional success.
4. Make restart failure visible on the error page.
5. Keep retry available and present Exit as an explicit fallback.
6. Keep platform-specific restart types inside the Windows infrastructure layer.

## Non-goals

- Do not implement a new restart mechanism or background process launcher.
- Do not attempt to infer whether Windows will accept restart before calling the API.
- Do not change normal application exit semantics.
- Do not add platform failure details to telemetry or user-facing text.
- Do not redesign settings restart prompts.

## Platform contract

The Windows App SDK restart call completes synchronously. A successful request terminates and relaunches the process; execution continues only when the API returns an `AppRestartFailureReason`. Therefore:

- teardown must not run before the restart call;
- a returned value always represents failure;
- the existing process must retain `IsShuttingDown == false` and must not cancel the global token after a returned failure;
- the adapter must preserve all four documented values: `RestartPending`, `NotInForeground`, `InvalidUser`, and `Other`.

## Functional requirements

### Windows infrastructure

- Wrap `AppInstance.Restart` in an injectable, infrastructure-local adapter.
- Reject a new attempt immediately if shutdown is already in progress.
- Track the restart request once before calling the platform adapter.
- Log a bounded diagnostic for every returned failure reason.
- Return `false` without calling teardown, cancelling application work, or changing shutdown state.
- Preserve teardown exclusively for explicit shutdown.

### Application use cases

- `RestartApplicationUseCase` must set `RestartApplicationResponse.Succeeded` from `IShutdownHandler.TryRestart`.
- `RestartSettingsApplicationUseCase` must do the same for its response.
- Existing `CanExecute` behavior must continue to reject attempts during actual shutdown.

### Error-page recovery

- Clear stale restart-failure UI when a new attempt begins.
- Show a bounded failure message when the use-case response is unsuccessful or unavailable.
- Leave Restart available for another attempt.
- Offer Exit as an explicit fallback action.
- Do not expose the platform failure reason to the UI or telemetry.

## Reliability requirements

- Every returned `AppRestartFailureReason` must leave global cancellation untouched.
- Every returned `AppRestartFailureReason` must leave `IsShuttingDown` false.
- An attempt made during real shutdown must not invoke the platform adapter.
- Failure feedback must be driven by the use-case response, not by timing assumptions.
- The new adapter must not be exposed through application abstractions.

## Test plan

- Parameterize Windows handler tests over all four `AppRestartFailureReason` values.
- For each value, verify `false`, no global cancellation, and no shutdown-state transition.
- Verify shell and settings restart use cases propagate both success and failure results.
- Verify the error-page view model displays failure and retains retry/exit actions.
- Build the WinUI project to validate dependency injection and XAML bindings.
- Run every non-UI test project, including the new Windows infrastructure test project.

## Acceptance criteria

- [x] A rejected restart no longer tears down the running process.
- [x] All documented Windows restart failure reasons have regression coverage.
- [x] Restart responses reflect the handler result.
- [x] The error page reports failure and offers retry plus Exit.
- [x] Existing non-UI tests remain green.
- [x] The WinUI x64 Debug project builds successfully.

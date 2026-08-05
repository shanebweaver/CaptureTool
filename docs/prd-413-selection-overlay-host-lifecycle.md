# PRD: Selection Overlay Host Lifecycle Cleanup

- Issue: [#413](https://github.com/shanebweaver/CaptureTool/issues/413)
- Architecture finding: `ARCH-08`
- Severity: Medium
- Status: Implemented
- Affected feature: `CAP-01`

## Summary

`SelectionOverlayHost.Close` and `SelectionOverlayHost.Dispose` must converge on one idempotent cleanup path. Disposing the host without an earlier explicit close must stop foreground monitoring, detach and dispose view models, close overlay windows, and release monitor capture buffers exactly once.

## Problem

`SelectionOverlayHost.Dispose` currently marks `_disposed` before calling `Close`. `Close` immediately returns when `_disposed` is true, so a dispose-only caller skips every resource cleanup step. The normal navigation path calls `Close` before `Dispose`, which masks the defect during ordinary use.

A dispose-only or exceptional teardown path can therefore retain native overlay windows, XAML islands, timer subscriptions, view-model subscriptions, window handles, and large per-monitor pixel buffers.

## Goals

1. Make dispose-only teardown perform the same cleanup as an explicit close.
2. Execute host cleanup at most once across any sequence of `Close` and `Dispose` calls.
3. Keep cleanup safe when initialization stopped before any resources were created.
4. Preserve the existing best-effort handling for individual overlay-window close failures.
5. Add non-UI lifecycle regression tests that do not require native windows or monitor capture.

## Non-goals

- Redesigning overlay creation, activation, focus monitoring, or capture behavior.
- Changing `SelectionOverlayWindow` or `CaptureOverlayHost` ownership.
- Adding finalizers for UI or native resources.
- Making WinUI and Win32 resources safe to tear down from arbitrary threads.
- Changing application navigation semantics.

## Functional requirements

### Unified close path

1. Move resource teardown into a private `CloseCore` method.
2. `Close` invokes `CloseCore` through a close-once lifecycle boundary.
3. `Dispose` invokes the same lifecycle boundary before suppressing finalization.
4. Disposal state must not prevent the first close operation from running.

### Idempotence

1. Dispose-only calls cleanup once.
2. Close followed by Dispose calls cleanup once.
3. Repeated Dispose calls cleanup once.
4. Repeated Close calls cleanup once.
5. Close after Dispose is a no-op.
6. The close-once transition is atomic so reentrant or competing calls cannot enter cleanup twice.

### Resource release

The existing cleanup responsibilities remain intact:

- stop and detach the foreground timer;
- clear the primary-window reference;
- detach and dispose the host view model;
- close every overlay window on a best-effort basis;
- clear window, handle, and monitor collections so captured pixel buffers can be reclaimed.

## Reliability requirements

- An uninitialized host can be closed or disposed without throwing.
- Lifecycle state changes before cleanup begins so reentrant close notifications cannot duplicate teardown.
- One overlay-window close failure does not prevent remaining windows and collections from being cleaned.
- `GC.SuppressFinalize` is called for every first successful disposal request even though the class has no finalizer today.

## Test plan

Add focused tests for the lifecycle boundary:

- Dispose-only invokes cleanup once and records closed/disposed state.
- Close then Dispose invokes cleanup once.
- Repeated Dispose invokes cleanup once.
- Repeated Close and Close-after-Dispose invoke cleanup once.
- A no-op cleanup delegate represents a partially initialized host and completes safely.
- A reentrant Close from inside cleanup does not invoke cleanup again.

Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] `SelectionOverlayHost.Dispose` cannot skip host cleanup.
- [x] All Close/Dispose call orders are idempotent.
- [x] Partially initialized hosts tear down safely.
- [x] Overlay cleanup responsibilities remain unchanged.
- [x] Lifecycle regression tests pass.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No migration or feature flag is required. The corrected lifecycle behavior applies whenever a selection overlay host is closed or disposed.

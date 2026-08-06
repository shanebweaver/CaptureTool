# PRD: Bound diagnostics log rendering to the view lifetime

- Issue: [#424](https://github.com/shanebweaver/CaptureTool/issues/424)
- Architecture finding: `ARCH-19`
- Severity: Medium
- Status: Implemented
- Affected features: `PLT-07`

## Summary

The diagnostics view model must receive live log updates only while its view is active, apply those updates on the UI task environment, and retain a bounded amount of rendered log text. Closing diagnostics deterministically removes its singleton log-service subscription through the existing view-unload disposal path.

## Problem

`DiagnosticsViewModel` subscribes to the singleton `ILogService` in its constructor and removes the handler only from a finalizer. The event subscription itself holds the view model alive, so the finalizer cannot run and every diagnostics instance remains subscribed after its view closes.

The log service raises `LogAdded` synchronously on the thread that produced the log. The handler directly changes the UI-bound `Logs` property, allowing background capture or finalization work to raise property changes off the UI thread. Each update also concatenates onto an unbounded string for as long as a leaked instance remains alive.

## Goals

1. Remove the live-log subscription deterministically when diagnostics closes.
2. Marshal live log rendering through `ITaskEnvironment`.
3. Prevent callbacks queued before disposal from changing disposed view-model state.
4. Bound displayed history to the newest 1,000 entries, matching the short-term log service limit.
5. Preserve logging enablement, clear, and export behavior.

## Non-goals

- Do not change the log service's retention period or persistence behavior.
- Do not redesign diagnostics as a virtualized item collection.
- Do not alter the formatting or export format of log entries.
- Do not keep diagnostics subscribed while its view is unloaded.

## Functional requirements

### Deterministic lifetime

- `DiagnosticsViewModel.Dispose` removes the `LogAdded` handler exactly once.
- The existing `ViewBase.OnUnloaded` call to `ViewModel.Dispose` owns diagnostics cleanup.
- Disposal remains safe when called more than once.
- A live log callback that was queued before disposal becomes a no-op after disposal.

### UI-thread delivery

- A `LogAdded` notification requests execution through `ITaskEnvironment`.
- The `Logs` property changes only inside the dispatched action.
- A notification received after disposal does not request UI work.

### Bounded rendering

- Initial and live history retain at most the newest 1,000 rendered entries.
- When the limit is exceeded, the oldest displayed entries are removed first.
- Clearing logs also clears the view model's bounded rendered history.

## Reliability requirements

- The singleton log service must not retain diagnostics view models after their views unload.
- Background log producers must not directly raise `Logs` property changes.
- Disposal races must not restore or append text to a closed diagnostics surface.
- If the platform task environment rejects dispatch, the notification is dropped without an off-thread fallback.

## Test plan

- Raise a live log notification and verify text remains unchanged until the task-environment action executes.
- Queue a notification, dispose twice, and verify the subscription is removed once and queued or later updates are ignored.
- Raise more than 1,000 notifications and verify only the newest 1,000 are rendered.
- Run all non-UI tests and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Closing diagnostics deterministically unsubscribes its view model.
- [x] Live log updates are applied through the UI task environment.
- [x] Disposed diagnostics instances ignore queued and future log updates.
- [x] Rendered log history remains bounded to the newest 1,000 entries.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

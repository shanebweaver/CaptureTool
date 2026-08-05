# PRD: Commit navigation only after host acceptance

- Issue: [#409](https://github.com/shanebweaver/CaptureTool/issues/409)
- Severity: High
- Status: Ready for implementation
- Affected features: `APP-02`, `APP-07`, `CAP-05`

## Summary

Navigation history, completion telemetry, and the `Navigated` event must describe the route the UI host actually accepted. Every navigation operation will prepare a candidate transition, await an explicit host result, and commit application navigation state only when the host accepts it.

The change replaces the synchronous `void` navigation dispatch contract with awaited results, removes `async void` from the top-level WinUI navigation handler, and preserves the serialization and leave-policy behavior introduced by #408.

## Problem

`NavigationService` currently mutates its stack before calling `INavigationHandler`. The handler returns `void`, while `AppNavigationHandler` is implemented as `async void` and can reject a request later when the user declines cancellation of an active video recording. It can also fail after an asynchronous host operation.

The service therefore reports navigation as complete before acceptance is known. A rejected transition can leave the recording overlay visible while `CurrentRequest` names a different route, causing route-based commands to disable controls that are still on screen. Exceptions escape the awaited use-case boundary, and telemetry/events can claim a transition that did not happen.

## Goals

1. Keep `CurrentRequest`, back history, telemetry, and `Navigated` synchronized with accepted host state.
2. Make host rejection, no-change, cancellation, and failure explicit to navigation callers.
3. Await WinUI host transitions so exceptions return through the initiating use case or activation boundary.
4. Preserve #408's edit/audio leave policy and single-transition serialization.
5. Preserve existing accepted navigation, back, clear-history, protocol activation, and capture-completion behavior.

## Non-goals

- Do not redesign edit or capture confirmation UI.
- Do not change which routes are hosted by the main window, selection overlay, or capture overlay.
- Do not persist navigation history across application launches.
- Do not add arbitrary retry behavior after platform/window failures.
- Do not claim that every platform side effect can be reversed after a host throws; the application navigation model must remain uncommitted, and the host must recover to a usable state where practical.

## Navigation result contract

Add an explicit result with these outcomes:

- **Accepted:** the host accepted the requested route and application navigation state may commit.
- **Rejected:** the host deliberately retained its current UI, such as when active-video cancellation is declined.
- **No change:** no target exists or the requested route/parameter already matches the current request.

Exceptions and cancellation remain exceptions/cancellation rather than being collapsed into rejection, allowing existing awaited boundaries to log or classify them correctly.

## Functional requirements

### Navigation service transaction

For navigate, back, and back-to operations:

1. Serialize the entire operation.
2. Inspect current history and prepare a candidate request without mutating history.
3. Return No change without dispatch when the request is an exact duplicate or a back target is unavailable.
4. Await `INavigationHandler` with the caller's cancellation token.
5. On Accepted, commit the prepared history mutation, then emit navigation-completed telemetry and `Navigated`.
6. On Rejected, cancellation, or exception, leave history unchanged and emit neither completion telemetry nor `Navigated`.

State queries must remain safe while an asynchronous transition is pending and must continue to expose the last committed request.

### Application coordinator and callers

- `INavigationCoordinator` continues to expose its existing Boolean accepted/not-accepted API to application use cases.
- It awaits the low-level navigation result and reports true only for an accepted transition; its exact-request shortcut remains successful without dispatch.
- Back operations report false for Rejected or No change.
- Trusted post-capture transitions still bypass abandonment guards, but they must await host acceptance.
- Error and UI-test bootstrap navigation must use the awaited service contract rather than fire-and-forget synchronous dispatch.

### WinUI host acceptance

- Replace `AppNavigationHandler.HandleNavigationRequest` `async void` with an awaited method.
- Thread cancellation through host serialization and active-video cancellation.
- Return Rejected before changing hosts when video-capture cancellation is declined.
- Return Accepted only after the requested top-level host has been created/updated or the main-window route has been handed to the main-window host.
- Return Rejected when an already-active capture host cannot apply a different request.
- Allow exceptions from host creation, disposal, or route dispatch to propagate to the awaited caller; keep `_activeHost` aligned with the host that remains usable where practical.

## Reliability requirements

- A rejected transition cannot alter stack count, `CurrentRequest`, or `CanGoBack`.
- A rejected transition cannot emit `navigation.completed` or raise `Navigated`.
- A handler exception or cancellation cannot alter navigation history or emit completion signals.
- The navigation serialization gate is released after acceptance, rejection, no-change, cancellation, or exception.
- A later request can succeed after any rejected or failed request.
- No top-level navigation handler remains `async void`.

## Test plan

### Navigation service

- accepted navigation commits history, telemetry, and event after handler completion;
- handler rejection retains the current request and history and emits no completion signals;
- handler exception and cancellation retain state and release serialization;
- pending host acceptance leaves `CurrentRequest` unchanged;
- rejected back and back-to retain the original stack;
- accepted back and clear-history preserve existing semantics;
- exact duplicate and missing targets return No change without host dispatch.

### Coordinator and use cases

- coordinator returns false for host rejection and true for acceptance;
- guards still run once before host dispatch;
- concurrent requests remain serialized through host completion;
- trusted audio/video completion use cases await and expose rejected navigation results.

### WinUI integration/build

- active video cancellation rejection returns Rejected before overlay disposal or host switch;
- accepted host paths return Accepted;
- all non-UI test projects pass;
- the WinUI x64 Debug project builds without `async void` navigation dispatch.

## Acceptance criteria

- [ ] Declining active-video cancellation leaves the capture overlay route committed and its controls eligible.
- [ ] History, telemetry, and `Navigated` commit only after host acceptance.
- [ ] Rejection, cancellation, and host failure leave the last committed navigation state intact.
- [ ] Navigation exceptions flow through awaited use-case/activation error boundaries.
- [ ] Navigate, back, and back-to operations use the same transactional contract.
- [ ] #408 leave guards and transition serialization remain intact.
- [ ] Existing non-UI tests pass and the WinUI x64 Debug project builds.

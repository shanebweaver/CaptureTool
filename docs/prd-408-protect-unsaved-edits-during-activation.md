# PRD: Protect unsaved edits during protocol activation

- Issue: [#408](https://github.com/shanebweaver/CaptureTool/issues/408)
- Severity: High
- Status: Ready for implementation
- Affected features: `APP-01`, `APP-02`, `APP-07`

## Summary

Every ordinary application transition that can replace the current image or video editor must evaluate the same asynchronous leave policy before navigation or shutdown begins. This includes redirected `ms-screenclip` activations, menu commands, nested open-file/recent-capture flows, application launch targets received by an already-running process, and window/application exit.

The change will add an application-level navigation coordinator. The coordinator will serialize transition attempts, evaluate the existing unsaved-edit and active-audio-capture guards exactly once, and delegate accepted transitions to the existing navigation service. Protocol activation will continue to route through the existing application use cases, which will now be protected at their navigation boundary.

## Problem

The application already knows how to ask whether a dirty edit session should be saved, discarded, or retained. That policy is invoked manually by selected menu commands and window close, but it is not part of the navigation boundary itself.

`CaptureToolActivationHandler` routes Print Screen and screen-recording protocol activations through `OpenSelectionOverlayUseCase` or `ShowHomePageUseCase`. Those use cases currently protect active audio capture only. They can navigate away from a dirty image or video editor without asking the user, after which the editor and its unsaved work are disposed.

Manual presentation-layer checks also make correctness depend on the caller and can prompt twice when a guarded command calls another guarded operation.

## Goals

1. Prevent redirected protocol activation from silently replacing a dirty image or video editor.
2. Give Save, Save As, Discard, and Cancel decisions the same meaning regardless of whether navigation began from a menu, protocol activation, nested use case, or window close.
3. Put leave-policy evaluation at one awaited application boundary instead of duplicating it in presentation commands.
4. Serialize competing transition attempts so only one confirmation and one accepted navigation can be in flight at a time.
5. Preserve all existing clean-session, warnings-disabled, and active-audio-capture behavior.
6. Establish a coordinator seam that #409 can later extend with asynchronous host acceptance and transactional navigation commit.

## Non-goals

- Do not implement #409's asynchronous WinUI host-acceptance result, navigation-history rollback, or post-acceptance telemetry changes.
- Do not redesign the confirmation UI or change its localized strings.
- Do not change save/export behavior inside image or video edit sessions.
- Do not redesign video-capture-overlay cancellation; its specialized recording-discard workflow remains intact.
- Do not force emergency error navigation, UI-test bootstrap navigation, or the WinUI host adapter through user-facing leave confirmation.

## User experience

When a protocol activation arrives while an image or video edit has unsaved changes:

- **Save / Save As:** run the session's existing save operation. Navigate only when it succeeds.
- **Discard:** proceed to the requested image or video selection overlay, or Home for an unknown protocol source.
- **Cancel:** keep the current editor and do not open the requested destination.
- **Save cancellation or failure:** keep the current editor and do not navigate.
- **Warnings disabled:** preserve the existing preference and navigate without a prompt.

Clean sessions continue immediately without a dialog. Repeated or competing requests must not display overlapping confirmation dialogs.

## Functional requirements

### Central transition coordinator

Add an application abstraction and implementation that:

- exposes awaited navigation, back-navigation, and leave/exit operations;
- serializes guard evaluation and transition dispatch;
- evaluates `IEditSessionGuard` before `IAudioCaptureNavigationGuard`, matching current window-close behavior;
- stops immediately when either guard rejects or cancellation is requested;
- delegates accepted transitions to `INavigationService` without changing that service's current synchronous contract;
- avoids prompting for an exact no-op navigation request;
- returns whether the transition was accepted/dispatched.

### Caller migration

- Route ordinary navigation initiated in `CaptureTool.Application` through the coordinator.
- Keep `INavigationService` available for navigation state queries and handler registration where needed.
- Remove presentation-level edit-guard checks from `AppMenuViewModel`; its commands must rely on the application use cases they invoke.
- Route application exit through the coordinator's leave policy before calling shutdown.
- Route `MainWindow` close confirmation through the same coordinator rather than invoking edit and audio guards independently.
- Preserve explicit raw-navigation exceptions for host/bootstrap/error-only paths and document them in code where ambiguity would remain.

### Activation behavior

- Print Screen and image hotkey activations retain image selection behavior after the leave policy accepts.
- Screen Recorder and `type=recording` activations retain video selection behavior after the leave policy accepts.
- Unknown `ms-screenclip` sources retain Home behavior after the leave policy accepts.
- Unsupported URI schemes remain ignored without initializing or prompting.
- Activation telemetry remains recorded using the current normalized source values.
- Exceptions remain bounded by the activation handler's existing logging boundary.

## Reliability requirements

- A rejected transition must not call `INavigationService.Navigate`, `TryGoBack`, `TryGoBackTo`, or shutdown.
- A failed/cancelled save must be treated as a rejected transition.
- The coordinator's serialization gate must always be released after success, rejection, cancellation, or exception.
- Removing presentation checks must not reduce protection for open file, recent capture, Home, capture, audio capture, Store, Settings, About, or Exit commands.

## Test plan

### Coordinator tests

- clean/no active edit session navigates without prompting;
- dirty session Save/Save As success navigates;
- dirty session Save/Save As failure does not navigate;
- dirty session Discard navigates;
- dirty session Cancel does not navigate;
- warnings-disabled dirty session navigates without prompting;
- active audio guard rejection does not navigate;
- exact no-op navigation does not prompt;
- concurrent transition attempts are serialized;
- guard exceptions/cancellation release the serialization gate.

### Activation regression tests

Use real coordinator/guard behavior with fake image and video `IEditableSession` instances:

- image protocol activation + dirty image session: Save, Discard, and Cancel;
- recording protocol activation + dirty video session: Save, Discard, and Cancel;
- unknown protocol activation + dirty session obeys the same policy before Home;
- rejected decisions never reach the navigation service.

### Existing behavior

- update application-use-case tests to assert coordinator calls and rejection responses;
- update `AppMenuViewModel` tests to prove commands delegate without a presentation-owned edit guard;
- run all non-UI test projects;
- build the WinUI x64 Debug project.

## Acceptance criteria

- [ ] A dirty image edit cannot be replaced by image/video protocol activation without the configured leave decision.
- [ ] A dirty video edit cannot be replaced by image/video protocol activation without the configured leave decision.
- [ ] Save proceeds only after a successful save; failed or cancelled saves retain the editor.
- [ ] Discard proceeds and Cancel retains the editor.
- [ ] Menu, nested use-case, protocol, and exit paths share the same application leave-policy boundary.
- [ ] Competing transitions cannot display overlapping leave confirmations.
- [ ] No ordinary application navigation caller can bypass the coordinator unintentionally.
- [ ] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Follow-up boundary

Issue #409 remains responsible for changing host dispatch from `void` to an awaited acceptance result, committing navigation history/telemetry only after host acceptance, and rolling back rejected or failed host transitions. This PR prepares that work by centralizing callers without claiming those guarantees prematurely.

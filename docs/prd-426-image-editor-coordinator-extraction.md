# PRD: Extract image editor interaction and operation coordinators

- Issue: [#426](https://github.com/shanebweaver/CaptureTool/issues/426)
- Architecture finding: `ARCH-21`
- Severity: Medium
- Status: Implemented
- Affected features: Image editor

## Summary

The image editor must retain `ImageEditSession` as its edit aggregate while moving reusable interaction and asynchronous-operation policy out of the oversized WinUI control and page view model. Point-based AI tools use one mutually exclusive interaction controller, all cancellable image operations use one keyed lifecycle coordinator, and shape snapshots become an independent domain value rather than a nested type on the obsolete canvas-operation hierarchy.

## Problem

`ImageCanvas.xaml.cs` coordinates viewport input, crop, shapes, text, color sampling, OCR overlays, and three nearly identical point-based AI tools. `ImageEditPageViewModel.cs` separately owns six cancellation-token-source fields and repeats cancel, stale-result, completion, and disposal logic for every AI workflow. Current edit commands and UI contracts also refer to `ModifyShapeOperation.ShapeState`, coupling active editor code to a legacy undo abstraction that has been superseded by `ImageEditSession` and `IImageEditCommand`.

These duplicated policies make unrelated tools easy to interfere with. A newly started operation can leave an older continuation able to update shared state unless each workflow reproduces identity checks correctly, and every point tool must reproduce the same pointer validation and coordinate routing.

## Goals

1. Give cancellable image operations one keyed owner for cancellation, current-generation checks, completion, and disposal.
2. Route foreground extraction, object erase, and object extraction through one mutually exclusive point-selection controller.
3. Keep WinUI-specific pointer and cursor adaptation in `ImageCanvas` while making routing policy independently testable.
4. Make `ShapeState` a standalone domain snapshot used by the current command/session pipeline.
5. Preserve existing user-visible editor behavior, telemetry, history, recovery messages, and tool priority.

## Non-goals

- Do not redesign `ImageEditSession` or the current `IImageEditCommand` history.
- Do not change AI models, availability checks, consent prompts, or output semantics.
- Do not move rendering resources out of WinUI in this change.
- Do not redesign crop, shape, text, color-picker, or OCR interactions beyond removing their dependency on the point-tool routing duplication.
- Do not change XAML layout or visual styling.

## Functional requirements

### Cancellable operation lifecycle

- Starting an operation cancels and disposes the previous generation for the same key.
- Operations with different keys remain independent.
- A lease exposes its cancellation token and whether it is still the current generation.
- Completing or disposing a stale lease cannot clear a newer generation.
- Disposing the coordinator cancels every active operation and prevents new work.
- View-model continuations update running state or shared editor state only when their lease remains current.

### Point-selection interaction

- Exactly one point-selection mode is active: foreground extraction, object erase, object extraction, or none.
- Existing priority is preserved when inconsistent dependency-property state is observed: object extraction, then object erase, then foreground extraction.
- Secondary clicks and clicks outside the rendered image are ignored.
- Accepted clicks emit a mode plus image-canvas position; WinUI remains responsible for pointer capture, display-to-image conversion, and raising the existing public event.
- Cursor and touch-lock decisions consume the same resolved active-mode state as pointer routing.

### Shape snapshot contract

- `ShapeState` is a top-level domain value in `CaptureTool.Domain.Edit.Operations`.
- Current edit commands, `ImageEditSession`, presentation code, WinUI, and tests do not depend on `ModifyShapeOperation.ShapeState`.
- Snapshot and application behavior for rectangles, ellipses, lines, arrows, text, and unknown drawables is preserved.

## Reliability requirements

- Canceling one AI feature must not cancel another feature's active work.
- Late continuations from canceled or superseded work must not clear the running state of the replacement operation.
- View-model disposal must cancel all active editor operations deterministically.
- Point-tool routing must remain deterministic even if multiple mode dependency properties are transiently true.
- Existing failure messages provide recovery guidance and remain unchanged.

## Test plan

- Verify same-key replacement cancellation, cross-key independence, stale completion, explicit cancellation, and coordinator disposal.
- Verify point-mode priority and acceptance/rejection for inactive, secondary-button, outside-image, and valid primary-button input.
- Update shape snapshot and edit-session tests to exercise the standalone `ShapeState` contract.
- Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] The page view model owns no per-feature `CancellationTokenSource` fields.
- [x] Point-based AI routing is represented once and covered without WinUI automation.
- [x] Active editor code no longer refers to nested legacy shape state.
- [x] Existing image-edit behavior and recovery paths remain intact.
- [x] All non-UI tests pass and the WinUI x64 Debug project builds.

# PRD: Separate the capture toolbar shadow from pointer input

- Severity: Low
- Status: Proposed
- Affected feature: `CAP-05`

## Summary

The post-selection video capture toolbar must use separate native surfaces for interactive content and visual overflow. The interactive `CaptureOverlayHost` toolbar window will match the laid-out bounds of its active XAML content, while a companion layered window immediately behind it will render the existing controllable composition shadow.

The shadow window will be non-activating and fully click-through, allowing pointer input in every shadow-only pixel to reach the application underneath. The two windows will be measured, positioned, shown, hidden, and destroyed as one logical toolbar surface.

## Problem

`CaptureOverlayHost` currently creates one fixed-size topmost window for `CaptureOverlayView`. `CaptureOverlayView` renders a custom composition shadow inside that window using a 12-DIP blur radius, 0.3 opacity, and a `(0, 4)` DIP offset.

Composition content is clipped to its native window. Showing the shadow therefore requires transparent client-area padding around the visible toolbar. Although that padding has no visible XAML content, it remains part of the interactive HWND and can intercept pointer input intended for the captured application. XAML hit-testing cannot change that native input boundary.

The fixed `468` by `76` logical-pixel host also combines content sizing with shadow overflow, making state-driven width changes, localization, text scaling, and mixed-DPI sizing more fragile.

The affected surface is the post-selection capture and recording toolbar hosted by `CaptureOverlayHost`. It is not the monitor-sized `SelectionOverlayWindow` or its initial capture-selection toolbar.

## Goals

1. Make the interactive capture-toolbar HWND match the rectangular layout bounds of the active visible XAML content without any shadow gutter.
2. Preserve direct control over the existing shadow blur, opacity, offset, color, and rendering behavior while intentionally removing edge clipping.
3. Render the shadow outside the interactive HWND without blocking pointer input or taking activation.
4. Keep the toolbar and shadow aligned through first layout, capture-state changes, localization, text scaling, mixed DPI, and negative monitor coordinates.
5. Preserve existing toolbar focus, keyboard, flyout, recording, error, capture-border, theme, and capture-exclusion behavior.
6. Make creation, activation, layout updates, partial-failure cleanup, close, and disposal deterministic and idempotent.

## Non-goals

- Redesigning `CaptureOverlayToolbar`, its commands, or its acrylic surface.
- Changing the full-monitor `SelectionOverlayWindow` or `SelectionOverlayToolbar`.
- Changing capture selection geometry, recording targets, recorder behavior, or navigation semantics.
- Changing the appearance or normal behavior of `CaptureOverlayBorder`.
- Changing the discard-confirmation overlay workflow.
- Adding an end-user setting for shadow parameters.
- Replacing the custom shadow with DWM or `CS_DROPSHADOW` behavior.
- Creating a general-purpose window-effects framework or migrating all raw HWND hosts to `AppWindow`.
- Matching the interactive HWND to the toolbar's per-pixel rounded-corner silhouette; matching its rectangular XAML layout bounds is sufficient.
- Redesigning the toolbar for a localization or text-scale combination whose desired width exceeds the available monitor width.
- Changing the existing 14-physical-pixel top placement; normalizing that inset to DIPs is a separate change.
- Supporting toolbar dragging between monitors or teardown from arbitrary threads.

## Window model

`CaptureOverlayHost` will own three independent native surfaces:

| Surface | Content and bounds | Input and activation |
| --- | --- | --- |
| Toolbar window | `CaptureOverlayView`, sized to the active toolbar or error content | Interactive and eligible for foreground activation |
| Shadow window | Shadow composition only, sized to the toolbar bounds plus a safe visual gutter | Fully click-through and never activated |
| Capture-border window | Existing `CaptureOverlayBorder` at the selected capture area | Existing behavior remains unchanged |

### Toolbar window

- Retain the existing popup, layered, topmost, and tool-window behavior.
- Remove the in-client shadow visual and all shadow-only padding.
- Measure the toolbar after XAML content is attached and remeasure it whenever visible content changes size.
- Do not depend on a final visible-window `Loaded` or `SizeChanged` event to bootstrap the first measurement. Use an explicit XAML measure/arrange pass or an equivalent content-size mechanism before final visible placement.
- Constrain initial measurement to the logical width available on the target monitor. Supported content that fits within that width must not be clipped.
- Keep the toolbar as the only member of the toolbar/shadow pair that can become active or foreground.

When `RecordingErrorInfoBar` replaces the toolbar, the interactive window will retain the last valid toolbar width, constrained to the target monitor, and grow vertically to the error content's measured height. This avoids circular stretch measurement and keeps the message and dismissal action usable. The shadow remains hidden for the error state.

### Shadow window

- Create a dedicated top-level layered, topmost tool window with `WS_EX_TRANSPARENT` and `WS_EX_NOACTIVATE`.
- Host a small shadow-only XAML view with a transparent, non-focusable, non-hit-testable root.
- Move the existing `DropShadow` and caster `SpriteVisual` ownership out of `CaptureOverlayView` and into the shadow view.
- Preserve the current black, 12-DIP blur, 0.3 opacity, and `(0, 4)` DIP offset.
- Centralize the effect values and a sufficient shadow gutter so host geometry and rendering cannot drift apart.
- Size and position the shadow caster to the toolbar rectangle within the inflated shadow surface.
- Render no duplicate toolbar, acrylic fill, controls, commands, or view model in this window.
- Use the layered transparent window style as the cross-process pass-through mechanism. Native transparent hit testing and XAML hit-test suppression may provide defense in depth but are not substitutes for that style.

The toolbar and shadow windows will remain unowned. The host will explicitly keep the shadow immediately below the toolbar in z-order and will reassert that ordering after toolbar activation and paired layout changes. The shadow must never be activated, shown alone, or included in task switching.

## Geometry and state requirements

One framework-independent calculation must derive both physical window rectangles from the target monitor bounds, measured toolbar size, XAML rasterization scale, existing physical top inset, and centralized shadow gutter.

The calculation must:

1. Anchor both windows to the monitor supplied in `NewCaptureArgs`.
2. Horizontally center the toolbar within that monitor while preserving the existing 14-physical-pixel top inset.
3. Round toolbar edges outward so fractional logical sizes cannot clip supported content.
4. Derive the shadow rectangle by inflating the completed physical toolbar rectangle rather than independently centering or rounding it.
5. Align the caster to that exact toolbar rectangle so fractional DPI cannot introduce a seam or drift.
6. Keep both windows hidden for zero, non-finite, or otherwise invalid measurements.
7. Ignore repeated layout reports that do not change physical bounds or visibility.

The host must remeasure and reposition the pair when toolbar content changes, including transitions among starting, recording, paused, resumed, stopped, and error states. Localization, text scaling, and audio controls that change the desired width must use the same path. Existing unconstrained toolbar flyouts must remain usable outside the tighter toolbar HWND.

## Input, focus, and capture requirements

- Pointer input within the toolbar rectangle continues to reach its XAML controls.
- Mouse, touch, and pen input in every shadow-only pixel reaches the underlying application, including partially opaque shadow pixels.
- Interacting with a shadow-only pixel does not activate CaptureTool or make the shadow foreground.
- Toolbar keyboard focus, accelerators, split buttons, sliders, and flyouts retain their current behavior.
- Apply `WDA_EXCLUDEFROMCAPTURE` to the new toolbar/shadow pair before their first visible placement. The existing border exclusion behavior remains in force.
- Captured output must not contain the toolbar, border, a detached shadow, or a blank replacement rectangle for the shadow window.

## Reliability requirements

- `Initialize`, `Activate`, `Close`, and `Dispose` remain safe and idempotent across repeated and mixed call orders.
- Explicit host lifecycle state, rather than the presence of every optional HWND, determines whether the pair has already been initialized.
- Calling `HideBorder` must not allow a later `Initialize` call to create a duplicate toolbar or shadow.
- Detach layout and visibility callbacks before teardown; queued or late callbacks after close cannot move, recreate, or reshow either window.
- Partial initialization failure releases every resource created before the failure, and one cleanup failure does not prevent remaining best-effort cleanup.
- Dispose each shadow composition object and `DesktopWindowXamlSource` before destroying its host HWND.
- Remove the current unbalanced toolbar `SizeChanged` subscription when shadow ownership moves out of `CaptureOverlayView`.
- If shadow creation or composition initialization fails, continue with a tightly sized usable toolbar and no shadow. Never restore transparent padding to the interactive window as a fallback.
- No failure or teardown path may leave the shadow visible without the toolbar.

## Test plan

### Automated tests

- Add pure geometry tests at 100%, 125%, 150%, and 200% scale, including fractional content sizes, negative monitor origins, centering, outward rounding, shadow inflation, caster alignment, invalid sizes, and stable repeated layouts.
- Cover dynamic toolbar sizes and the fixed-width/growing-height error policy.
- Add lifecycle coverage for repeated close/dispose calls, close before first measurement, late layout callbacks, partial initialization failures, cleanup failures, shadow-create fallback, and `HideBorder` followed by `Initialize`.
- Add a Windows integration or UI test with a controlled window from another process beneath the toolbar. Verify a shadow-only click reaches that window while a toolbar click reaches the intended control.
- Verify native bounds, required shadow styles, z-order, foreground ownership, and display affinity for the toolbar, shadow, and border.
- Extend the existing video-capture UI path through start, pause, resume, stop, error, error dismissal, and representative toolbar flyouts.

### Manual validation

- Inspect the complete shadow on every edge for clipping, seams, first-show flashes, or black frames.
- Validate mouse, touch, and pen pass-through over another application.
- Validate light, dark, and high-contrast themes; representative localizations and text scales; and primary, negative-origin, and mixed-DPI monitors.
- Confirm toolbar focus, keyboard controls, audio flyouts, and task switching remain correct.
- Inspect captured video to confirm that the toolbar, border, and shadow do not appear.
- Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [ ] The interactive toolbar HWND matches supported active-content bounds and contains no shadow-only dead pointer region.
- [ ] The existing configurable shadow is fully visible outside every toolbar edge without clipping or alignment drift.
- [ ] Mouse, touch, and pen input in shadow-only pixels reaches the underlying application without activating CaptureTool.
- [ ] Toolbar controls, focus, flyouts, recording transitions, and error UI retain their current behavior.
- [ ] Toolbar and shadow bounds remain aligned and centered at supported DPI scales and negative monitor coordinates.
- [ ] Toolbar, shadow, and border are absent from capture output; the shadow never appears in the taskbar or Alt+Tab.
- [ ] Repeated, partial, exceptional, fallback, and late-callback paths create no duplicate windows, leave no visible orphan shadow, and release each owned resource once.
- [ ] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No data migration or permanent feature flag is required. The toolbar and companion shadow will ship atomically, with best-effort shadow creation and a no-shadow fallback. The current padded interactive-window path must not remain as a fallback because it reintroduces the pointer-input defect.

# PRD: Clip cross-monitor window selections and preserve window identity

- Issue: [#414](https://github.com/shanebweaver/CaptureTool/issues/414)
- Finding: `ARCH-09`
- Severity: Medium
- Status: Proposed
- Affected features: `CAP-01`, `CAP-02`, `CAP-04`

## Summary

Window capture selection must show and submit only the portion of a window that is visible on the monitor hosting the selection overlay. The selected native window handle must travel with that selection as explicit identity rather than being recovered later by comparing rectangles.

The change will centralize physical-screen-to-monitor-canvas projection, clip on all four monitor edges, preserve sub-DIP intersections with deterministic rounding, and carry the handle and area together from the overlay control to the capture request.

## Problem

`SelectionOverlayHost` projects every intersecting native window into monitor-local logical coordinates without first clipping it to the monitor. The overlay control and view model compensate only for negative left and top coordinates. A window crossing the right or bottom edge therefore produces a selection rectangle outside the overlay canvas and can request a crop outside the captured monitor image.

For video window capture, the view model then searches the original window list for a rectangle equal to the completed selection rectangle. Clipping and DPI rounding can change that rectangle, so geometry is not a reliable identifier. A failed match silently produces a zero handle and changes the recording target from a native window to a monitor rectangle.

## Goals

1. Keep every selectable window rectangle within its monitor-local overlay canvas on the left, top, right, and bottom edges.
2. Preserve visible intersections when physical coordinates map to less than one logical pixel at non-96 DPI.
3. Carry the native window handle with the completed window selection.
4. Keep rectangle and full-screen capture selections handle-free.
5. Make the geometry rules independently testable without a WinUI runtime.

## Non-goals

- Do not change native window enumeration, z-order, or overlap precedence.
- Do not change the minimum 40-by-40 logical-pixel selection requirement.
- Do not combine portions of one window across multiple monitor captures.
- Do not redesign the selection overlay or toolbar.
- Do not change native window-recording behavior after a valid handle reaches the video workflow.

## Functional requirements

### Monitor projection and clipping

For each monitor/window pair:

1. Intersect the native window's physical screen rectangle with the monitor's physical bounds before projection.
2. Exclude windows with an empty physical intersection.
3. Translate the intersection relative to the monitor origin, including monitors with negative desktop coordinates.
4. Divide physical coordinates by the monitor scale to obtain overlay logical coordinates.
5. Round leading edges down and trailing edges up so a non-empty physical intersection remains non-empty after projection.
6. Clamp the result to the monitor's logical canvas bounds on all four edges.
7. Preserve the original native window handle and title on the projected entry.

The same projected entries must drive pointer hit testing, visual selection, and capture identity. The control must not apply a second, different clipping formula.

### Selection identity

A completed selection must contain:

- the monitor-local logical capture area; and
- the selected native window handle, or zero for rectangle/full-screen selections.

The view model must update these values atomically. Clearing a selection, changing capture options, or completing a non-window selection must clear any previous window handle. Starting video window capture must use the stored handle directly and must not search by rectangle equality.

### Multi-monitor coordination

When one overlay receives a non-empty selection, the other monitor overlays must clear both their area and selected handle. Existing primary/secondary coordination and all-screens behavior remain unchanged.

## Reliability and edge cases

- Windows crossing the left, top, right, or bottom edge are clipped to the visible monitor portion.
- Windows crossing two or more edges are clipped on every edge.
- Monitors with negative desktop origins use the same local coordinates as positive-origin monitors.
- Mixed-DPI projection cannot create negative sizes or coordinates outside the logical canvas.
- A one-physical-pixel intersection remains represented by at least one logical pixel before the existing minimum-selection rule is applied.
- Empty or non-intersecting windows are not selectable on that monitor.
- Duplicate projected rectangles retain distinct handles; the clicked entry supplies identity.

## Test plan

### Pure geometry tests

- window entirely within a 96-DPI monitor;
- clipping at each of the four edges;
- clipping at opposing/corner edges;
- negative-origin monitor translation;
- mixed-DPI projection with leading-edge floor and trailing-edge ceiling;
- one-pixel intersection at each trailing/leading edge;
- non-intersecting and edge-touching windows are excluded;
- original handle/title are preserved.

### View-model tests

- window selection sends its explicit handle to video capture even when another window has the same rectangle;
- rectangle/full-screen selection sends a zero handle;
- clearing or replacing a selection clears a stale handle;
- image capture continues to use the selected area.

### Build and regression validation

- run the Presentation test project;
- run all non-UI test projects;
- build the WinUI x64 Debug project.

## Acceptance criteria

- [ ] Cross-monitor window selections never extend outside the local overlay canvas.
- [ ] Left, top, right, bottom, negative-origin, mixed-DPI, and one-pixel intersections have regression coverage.
- [ ] Window identity is carried explicitly from pointer selection to `NewCaptureArgs`.
- [ ] Rectangle equality is no longer used to recover a selected handle.
- [ ] Clearing or changing selections cannot reuse a stale window handle.
- [ ] Existing non-UI tests pass and the WinUI x64 Debug project builds.

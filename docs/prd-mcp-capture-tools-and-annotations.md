# PRD: MCP Capture Tools and Annotations

## Summary

GitHub issue: TBD

Implementation status: planning started. This document defines the next image-focused MCP server expansion after `capture_primary_monitor`: region capture, all-screens capture, monitor listing and capture, window listing and capture, annotation of existing captures, and a combined primary-monitor-with-annotations capture tool.

The goal is to help agents provide precise visual proof. A raw screenshot says "look at this screen"; an annotated screenshot says "look exactly here." The first annotation set should support arrows and rectangles, and every annotation that includes a label must add a nearby `TextDrawable` so labels render through the same image pipeline as the rest of CaptureTool.

## Source Notes

- Initial MCP server PRD: `docs/prd-local-mcp-image-capture-server.md`
- MCP tools specification: https://modelcontextprotocol.io/specification/2025-06-18/server/tools
- MCP resources specification: https://modelcontextprotocol.io/specification/2025-06-18/server/resources

Important protocol constraints from the docs:

- Capture and annotation operations should be MCP tools because they are model-invoked actions.
- Tool results can return image content directly with PNG bytes.
- Tool results can also return structured metadata; capture tools should include enough coordinates and IDs for later annotation calls.
- Resources may be useful later for browsing or reopening recent captures, but this PRD keeps the first implementation tool-centered.

## Problem

The current MCP server can return a primary-monitor screenshot, but agents still have to describe the relevant part in prose. That is clumsy when the user needs proof of a particular button, error message, visual state, or region.

Agents also need more capture modes than the primary monitor:

- a rectangle region when the relevant area is small
- all screens when state spans displays
- a specific monitor when the primary monitor is not the active work area
- a specific window when the agent wants to avoid unrelated desktop content

Once a screenshot exists, agents need a way to mark it with arrows, rectangles, and labels without opening the full CaptureTool UI.

## Goals

- Add MCP image capture tools for region, all-screens, monitor, and window capture.
- Add listing tools for monitors and windows so agents can choose a target deliberately.
- Add an annotation tool that renders arrows and rectangles onto a captured image.
- Add a combined capture-and-annotate tool for primary-monitor screenshots.
- Reuse CaptureTool's existing screen capture and image drawable/rendering concepts where practical.
- Return PNG image content and structured metadata for every capture or annotation result.
- Keep all operations local and stdio-based.

## Non-Goals

- Video capture.
- Audio capture.
- OCR or AI image understanding.
- Remote or network-accessible capture.
- Interactive user-region selection UI through MCP.
- Full image editing through MCP.
- Persistent capture library management.
- Annotation types beyond arrow, rectangle, and label text in this phase.

## User Stories

- As a user, I can ask an agent to capture only the region that matters.
- As a user, I can ask an agent to show all screens when desktop state spans monitors.
- As an agent, I can list monitors and capture the intended monitor.
- As an agent, I can list windows and capture the intended window.
- As an agent, I can draw an arrow to a specific UI element in a screenshot.
- As an agent, I can draw a rectangle around a relevant area in a screenshot.
- As a user, I can read a nearby label attached to an arrow or rectangle.
- As a developer, I can test annotation placement without a real desktop capture.

## Current Architecture

- `CaptureTool.Mcp.CaptureServer` exposes the local stdio MCP server.
- `PrimaryMonitorCaptureService` captures the primary monitor through `IScreenCapture`.
- `IScreenCapture` exposes:
  - `CaptureAllMonitors`
  - `CombineMonitors`
  - `CreateBitmapFromMonitorCaptureResult`
  - `CreateCroppedBitmap`
  - `SaveImageToFile`
- `MonitorCaptureResult` includes monitor handle, pixel buffer, DPI, monitor bounds, work-area bounds, scale, and `IsPrimary`.
- `WindowInfoHelper` can enumerate windows through CaptureKit-backed infrastructure.
- Existing drawable domain types include:
  - `RectangleDrawable`
  - `ArrowDrawable`
  - `TextDrawable`
  - `ImageDrawable`
- `Win2DImageCanvasRenderer` and `Win2DImageCanvasExporter` already render drawables into PNG output for app image editing.

## Design Principles

- Tool names should be explicit and stable.
- Capture tools should return images directly, not require opening files.
- Each capture result should include a `captureId` so later tools can refer to it.
- IDs are process-local and temporary in this phase.
- Coordinates should be documented and echoed back in metadata.
- Annotation requests should be deterministic: the server should not infer target coordinates from prose.
- Labels must be rendered with a nearby `TextDrawable`, not baked through a separate drawing path.
- Window capture should minimize unrelated desktop content when possible.
- If a capture target cannot be found, return an MCP tool execution error, not a protocol error.

## Coordinate Model

The server should use three coordinate spaces:

- `virtualScreen`: Windows desktop coordinates across all monitors.
- `monitor`: coordinates relative to a selected monitor's top-left corner.
- `image`: coordinates relative to the top-left of the returned PNG.

Tool inputs should prefer `virtualScreen` for global capture tools and `image` for annotation tools.

Every capture result should include:

- `captureId`
- `capturedAtUtc`
- `sourceKind`
- `coordinateSpace`
- `imageBounds`
- `sourceBounds`
- `dpi`
- `scale`
- `format`

For all-screens capture, `sourceBounds` should be the combined virtual-screen rectangle.

## Tool Contracts

### 1. `capture_region`

Captures a rectangular region of the virtual desktop.

Input:

```json
{
  "x": 100,
  "y": 200,
  "width": 800,
  "height": 500,
  "coordinateSpace": "virtualScreen",
  "reason": "Show the failed dialog"
}
```

MVP decisions:

- `coordinateSpace` defaults to `virtualScreen`.
- `width` and `height` must be greater than zero.
- The region may span monitors.
- If a region spans monitors, capture all relevant monitors first and crop from the combined bitmap.
- Reject regions that do not intersect any monitor.

### 2. `capture_all_screens`

Captures all monitors into one combined PNG.

Input:

```json
{
  "reason": "Show the full desktop state"
}
```

MVP decisions:

- Use the existing monitor-combine behavior.
- Return combined virtual-screen bounds.
- Include monitor metadata for each monitor included in the result.

### 3. `list_monitors` and `capture_monitor`

Lists available monitors, then captures one selected monitor.

`list_monitors` input:

```json
{}
```

`list_monitors` output metadata:

```json
{
  "monitors": [
    {
      "monitorId": "hmonitor:65537",
      "index": 0,
      "isPrimary": true,
      "bounds": { "x": 0, "y": 0, "width": 2560, "height": 1440 },
      "workAreaBounds": { "x": 0, "y": 0, "width": 2560, "height": 1392 },
      "dpi": 144,
      "scale": 1.5
    }
  ]
}
```

`capture_monitor` input:

```json
{
  "monitorId": "hmonitor:65537",
  "reason": "Show the secondary display"
}
```

MVP decisions:

- `monitorId` is the preferred selector.
- `index` may be added later as an additional selector, but monitor ID should be first because it is less ambiguous within a running desktop session.
- `capture_monitor` should fail if the target monitor is no longer present.
- A primary-monitor shorthand can be supported later if useful, but `capture_primary_monitor` already exists.

### 4. `list_windows` and `capture_window`

Lists capturable windows, then captures one selected window.

`list_windows` input:

```json
{
  "includeMinimized": false
}
```

`list_windows` output metadata:

```json
{
  "windows": [
    {
      "windowId": "hwnd:197394",
      "title": "CaptureTool",
      "bounds": { "x": 100, "y": 80, "width": 1400, "height": 900 },
      "isVisible": true
    }
  ]
}
```

`capture_window` input:

```json
{
  "windowId": "hwnd:197394",
  "reason": "Show the app packaging page"
}
```

MVP decisions:

- `windowId` should be derived from the native window handle and treated as temporary.
- Exclude invisible, untitled, tool, shell, and zero-size windows by default.
- Minimized windows should be excluded in the MVP.
- If the window moves between list and capture, capture its current bounds.
- If CaptureKit window capture is not available for a target, fail clearly. Do not use desktop-bound cropping as a substitute.

### 5. `annotate_image`

Renders annotations onto a previous capture.

Input:

```json
{
  "captureId": "capture:01J...",
  "annotations": [
    {
      "type": "arrow",
      "start": { "x": 1200, "y": 340 },
      "end": { "x": 980, "y": 420 },
      "label": "Click this"
    },
    {
      "type": "rectangle",
      "x": 900,
      "y": 380,
      "width": 260,
      "height": 120,
      "label": "Error state"
    }
  ]
}
```

MVP annotation types:

- `arrow`
- `rectangle`

MVP styling defaults:

- Stroke color: red.
- Text color: white.
- Text background: semi-opaque red.
- Stroke width: scale-aware default, with a minimum of 3 px.
- Font family: `Segoe UI`.
- Font size: scale-aware default, with a minimum of 18 px.

Label behavior:

- If an annotation has `label`, create a `TextDrawable` near that annotation.
- For arrows, place the text near the arrow tail by default.
- For rectangles, place the text just above the rectangle when there is room, otherwise just below.
- Clamp text bounds to the image bounds.
- Return label rectangle metadata so the agent can reason about the final placement.
- Labels should be optional. An unlabeled arrow or rectangle is valid.

### 6. `capture_primary_monitor_with_annotations`

Captures the primary monitor and renders annotations in one tool call.

Input:

```json
{
  "reason": "Show where the package version is configured",
  "annotations": [
    {
      "type": "rectangle",
      "x": 700,
      "y": 330,
      "width": 280,
      "height": 80,
      "label": "Version fields"
    }
  ]
}
```

MVP decisions:

- Annotation coordinates use `image` coordinates for the captured primary-monitor image.
- The tool should return both the raw `sourceCaptureId` and the annotated `captureId`.
- This tool can internally compose `capture_primary_monitor` and `annotate_image` behavior.

## Capture Store

The annotation tools need a temporary in-memory capture store.

Requirements:

1. Store capture PNG bytes, metadata, and source image dimensions by `captureId`.
2. Store annotated captures as separate entries.
3. Keep captures process-local.
4. Use a small bounded cache, for example 20 captures or 256 MB, whichever comes first.
5. Evict oldest captures first.
6. Do not persist captures to disk by default.
7. Return a clear tool error if a requested `captureId` is no longer available.

## Functional Requirements

1. `capture_region` captures a positive-size rectangle from the virtual screen.
2. `capture_all_screens` returns one combined PNG across all monitors.
3. `list_monitors` returns stable-enough process-local monitor IDs and metadata.
4. `capture_monitor` captures a listed monitor by monitor ID.
5. `list_windows` returns capturable windows and metadata.
6. `capture_window` captures a listed window by window ID.
7. `annotate_image` renders arrows and rectangles onto a stored capture.
8. `annotate_image` adds a nearby `TextDrawable` for every annotation label.
9. `capture_primary_monitor_with_annotations` captures the primary monitor and annotates it in one call.
10. Each image-producing tool returns PNG image content and structured metadata.
11. Each image-producing tool stores its result in the temporary capture store.
12. Tool execution errors return `isError: true` with useful text content.

## Technical Requirements

1. Add shared MCP capture models for:
   - capture metadata
   - monitor metadata
   - window metadata
   - rectangle DTOs
   - point DTOs
   - annotation requests
   - annotation placement results
2. Add an `IMcpCaptureStore` abstraction with an in-memory implementation.
3. Refactor `PrimaryMonitorCaptureService` into shared capture services so other tools reuse image encoding, metadata creation, and result building.
4. Add a region capture service that can crop regions from one monitor or the combined virtual screen.
5. Add all-screens capture support using the existing monitor-combine path.
6. Add monitor listing and capture services.
7. Add window listing and capture services using CaptureKit-backed window enumeration/capture where available.
8. Add an annotation renderer that creates an `ImageDrawable` for the source capture plus `ArrowDrawable`, `RectangleDrawable`, and `TextDrawable` instances for annotations.
9. Reuse `Win2DImageCanvasExporter` or extract a renderer that can run cleanly from the MCP server without WinUI app assumptions.
10. Keep stdout reserved for MCP messages only.
11. Add SDK-based smoke tests for `tools/list` covering all new tools.
12. Add unit tests for coordinate validation, monitor/window selection, capture-store eviction, annotation-to-drawable mapping, and label placement.

## Security and Privacy Requirements

1. All capture tools remain opt-in through an explicit MCP tool call.
2. Tool descriptions must clearly state what desktop content they capture.
3. Region and window capture should minimize unrelated desktop content.
4. Window capture must not use a broader capture path as a substitute.
5. No captures are persisted to disk by default.
6. Capture IDs are temporary and should not be treated as durable file references.
7. Diagnostic logs must not include image bytes or full OCR-like content.

## Proposed Implementation

### Phase 1: Shared Capture Result Model and Store

- Create common capture result models used by every MCP image tool.
- Add `captureId` generation.
- Add bounded in-memory capture store.
- Update `capture_primary_monitor` to store and return `captureId`.
- Update tests for existing primary-monitor capture metadata.

### Phase 2: Region Capture

- Add `capture_region`.
- Validate positive dimensions and monitor intersection.
- Crop from a single monitor when possible.
- Crop from combined monitors when the region spans displays.
- Return image content, metadata, and `captureId`.

### Phase 3: All-Screens Capture

- Add `capture_all_screens`.
- Combine all monitors.
- Include per-monitor metadata and combined virtual-screen bounds.
- Return image content, metadata, and `captureId`.

### Phase 4: Monitor Listing and Capture

- Add `list_monitors`.
- Add `capture_monitor`.
- Define monitor ID formatting.
- Return monitor metadata consistently across list and capture tools.
- Add tests for missing monitor and primary/secondary monitor selection.

### Phase 5: Window Listing and Capture

- Add `list_windows`.
- Add `capture_window`.
- Filter unsuitable windows by default.
- Capture by window ID.
- Add tests for filtering and missing-window handling.
- Manually validate with normal, elevated, minimized, and moving windows.

### Phase 6: Annotation Service

- Add annotation request models.
- Add mapping from annotation requests to drawables:
  - arrow request to `ArrowDrawable`
  - rectangle request to `RectangleDrawable`
  - label to nearby `TextDrawable`
- Add label placement and clamping logic.
- Render annotated images to PNG.
- Store annotated outputs as new captures.
- Return annotation placement metadata.

### Phase 7: Combined Primary Monitor Annotation Tool

- Add `capture_primary_monitor_with_annotations`.
- Internally capture primary monitor, then render annotations.
- Return both source and annotated capture IDs.
- Add tests that verify the combined tool uses the same annotation placement path as `annotate_image`.

### Phase 8: Manual Validation

- Validate region capture on single and multi-monitor setups.
- Validate all-screens capture with negative-coordinate monitors.
- Validate monitor capture on primary and secondary displays.
- Validate window capture for visible windows.
- Validate annotated arrow and rectangle output with labels.
- Validate returned images render in at least one MCP host.

## Work Items

### 1. Add Shared MCP Capture Store and Result Models

Goal: make every capture tool return consistent metadata and support follow-up annotation.

Scope:

- Add shared metadata models.
- Add `captureId`.
- Add bounded in-memory store.
- Update `capture_primary_monitor` to store captures.
- Return `captureId` in structured output.

Deliverables:

- Capture metadata models.
- `IMcpCaptureStore` and in-memory implementation.
- Updated primary-monitor tool tests.

Validation:

- Existing primary-monitor tool still returns PNG image content.
- Repeated captures generate distinct IDs.
- Eviction removes oldest captures.

Status: pending.

### 2. Implement `capture_region`

Goal: let agents capture only the relevant desktop rectangle.

Scope:

- Add region request model.
- Validate coordinates and dimensions.
- Support virtual-screen coordinates.
- Crop from one monitor or combined monitors.
- Store and return capture result.

Deliverables:

- `capture_region` MCP tool.
- Region capture service.
- Unit tests for validation, single-monitor crop, multi-monitor crop, and out-of-bounds behavior.

Validation:

- Returned PNG dimensions match requested region.
- Tool errors are returned for invalid regions.

Status: pending.

### 3. Implement `capture_all_screens`

Goal: capture the full desktop across all monitors.

Scope:

- Add all-screens tool.
- Combine all monitor captures.
- Include combined bounds and per-monitor metadata.
- Store and return capture result.

Deliverables:

- `capture_all_screens` MCP tool.
- Tests for combined monitor metadata and no-monitor failure.

Validation:

- Output dimensions match combined virtual-screen bounds.
- Metadata includes every monitor.

Status: pending.

### 4. Implement `list_monitors` and `capture_monitor`

Goal: let agents deliberately choose a monitor before capture.

Scope:

- Add monitor listing model.
- Add monitor ID formatting and parsing.
- Add capture-by-monitor ID.
- Return consistent monitor metadata.

Deliverables:

- `list_monitors` MCP tool.
- `capture_monitor` MCP tool.
- Tests for ID formatting, primary monitor metadata, secondary monitor capture, and missing monitor errors.

Validation:

- A monitor ID returned by `list_monitors` can be passed to `capture_monitor`.

Status: pending.

### 5. Implement `list_windows` and `capture_window`

Goal: let agents capture a specific visible window without unrelated desktop content.

Scope:

- Add window listing model.
- Add default window filtering.
- Add window ID formatting and parsing.
- Capture by window ID.
- Return window bounds metadata.

Deliverables:

- `list_windows` MCP tool.
- `capture_window` MCP tool.
- Tests for window filtering, ID parsing, capture success, and missing-window errors.

Validation:

- A window ID returned by `list_windows` can be passed to `capture_window`.
- Missing or minimized windows fail cleanly.

Status: pending.

### 6. Implement `annotate_image`

Goal: render arrows, rectangles, and nearby text labels onto previous captures.

Scope:

- Add annotation request models.
- Add arrow and rectangle annotation mapping.
- Add label-to-`TextDrawable` mapping.
- Add label placement rules.
- Render annotated output to PNG.
- Store annotated capture as a new capture ID.

Deliverables:

- `annotate_image` MCP tool.
- Annotation renderer/service.
- Tests for arrow drawable creation, rectangle drawable creation, label placement, label clamping, and missing capture errors.

Validation:

- Every labeled annotation creates a `TextDrawable`.
- Annotated result includes PNG image content and annotation placement metadata.

Status: pending.

### 7. Implement `capture_primary_monitor_with_annotations`

Goal: support the common "show this screen and point here" workflow in one MCP call.

Scope:

- Add combined tool request model.
- Capture primary monitor.
- Apply annotation renderer.
- Return both source and annotated capture IDs.

Deliverables:

- `capture_primary_monitor_with_annotations` MCP tool.
- Tests proving it shares annotation behavior with `annotate_image`.

Validation:

- Tool returns annotated PNG image content.
- Labels render through `TextDrawable`.
- Metadata identifies both source and annotated captures.

Status: pending.

### 8. Add MCP Smoke and Manual Validation Coverage

Goal: keep the expanded MCP surface discoverable and manually proven.

Scope:

- Extend SDK-based smoke test to assert every new tool appears in `tools/list`.
- Add manual validation checklist output to the PR or release issue.

Deliverables:

- Tool discovery smoke tests.
- Manual validation notes.

Validation:

- `tools/list` exposes all six feature areas.
- Returned images render in a real MCP host.

Status: pending.

## Acceptance Criteria

- `capture_region` returns a PNG for a requested desktop rectangle.
- `capture_all_screens` returns one combined PNG across monitors.
- `list_monitors` returns monitor IDs and metadata.
- `capture_monitor` captures a listed monitor.
- `list_windows` returns capturable windows.
- `capture_window` captures a listed window.
- `annotate_image` renders arrows and rectangles onto an existing capture.
- Labeled annotations create nearby `TextDrawable` labels.
- `capture_primary_monitor_with_annotations` captures and annotates in one call.
- Every image-producing tool returns image content and structured metadata.
- Tests cover validation, metadata, selection, annotation mapping, and tool discovery.

## Open Questions

- Should `capture_region` support monitor-relative coordinates in the first implementation, or only virtual-screen coordinates?
- Should monitor IDs be based on HMONITOR handles, stable indexes, or both?
- Should window IDs be raw HWND-derived strings, or should the server maintain temporary opaque IDs?
- Should annotation colors be configurable in the first version?
- Should labels have explicit placement overrides, or only automatic placement initially?
- Should annotated capture results include the original unannotated image content too, or only the annotated PNG and source capture ID?

## Risks

- Coordinate spaces can become confusing if metadata is incomplete.
- Window capture may include shadows, borders, occlusion, or protected content depending on the underlying capture path.
- Large all-screens captures can produce large MCP payloads.
- Annotation labels can obscure the target if placement and clamping are too naive.
- Reusing the app renderer from a console MCP process may expose WinUI/WinRT assumptions that need extraction.
- In-memory capture storage needs firm bounds to avoid unbounded memory growth.

## Tracking Checklist

- [ ] Add shared capture result models.
- [ ] Add bounded in-memory capture store.
- [ ] Update `capture_primary_monitor` to return and store `captureId`.
- [ ] Add `capture_region`.
- [ ] Add `capture_all_screens`.
- [ ] Add `list_monitors`.
- [ ] Add `capture_monitor`.
- [ ] Add `list_windows`.
- [ ] Add `capture_window`.
- [ ] Add annotation request models.
- [ ] Add arrow annotation rendering.
- [ ] Add rectangle annotation rendering.
- [ ] Add label rendering through `TextDrawable`.
- [ ] Add `annotate_image`.
- [ ] Add `capture_primary_monitor_with_annotations`.
- [ ] Extend MCP tool discovery smoke tests.
- [ ] Manually validate returned images in an MCP host.

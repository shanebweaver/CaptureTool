# PRD: Export-owned image render resources

- Issue: [#411](https://github.com/shanebweaver/CaptureTool/issues/411)
- Finding: `ARCH-06`
- Severity: Medium
- Status: Implemented
- Affected features: `IMG-01`, `IMG-09`, `IMG-11`, `IMG-15`

## Summary

Image export must render from the edit session's `ImageFile` inputs without depending on resources prepared by the WinUI `ImageCanvas`. Each export operation will load its image drawables into a short-lived Win2D resource scope created on the same device as the export render target, use those resources for the render, and dispose them when encoding completes.

The interactive canvas can continue to cache preview resources for its own lifetime. The shared renderer will no longer treat a missing image resource as a drawable that can be silently skipped.

## Problem

`Win2DImageCanvasRenderer` currently resolves each `ImageDrawable` through a `ConditionalWeakTable` populated asynchronously by the WinUI canvas `CreateResources` event. `Win2DImageCanvasExporter` calls the renderer without preparing resources of its own. If an output command runs before preview preparation completes, during device-resource recreation, or in a headless path where no canvas exists, the renderer omits the base image and still produces a successful output.

Copy, save, share, external editing, OCR, and image-description paths consume the exporter, so a successful-looking operation can receive an annotation-only or effectively blank raster. The dependency also couples application output to a particular control and GPU-resource lifetime.

## Goals

1. Make export independent of `ImageCanvas` creation and resource timing.
2. Load every exported `ImageDrawable` from its current `ImageFile` on the export render device.
3. Keep export resources scoped to one operation and dispose them on success or failure.
4. Fail explicitly when an image resource cannot be loaded or resolved.
5. Preserve orientation, crop, annotation, chroma-key, file-format, clipboard, and save behavior.

## Non-goals

- Do not move Win2D objects into domain or application-layer models.
- Do not replace the interactive canvas preview cache or redesign WinUI resource recreation.
- Do not add a user-visible render-readiness gate; export-owned loading removes the timing dependency.
- Do not redesign the broader image rendering pipeline or change output formats.

## Functional requirements

### Export resource scope

- Before drawing, collect the `ImageDrawable` instances in the requested drawable set.
- Load each distinct drawable's current file into a Win2D bitmap using the export render target's device.
- Resolve renderer image requests from the operation-owned resource map.
- Dispose all loaded bitmaps after output encoding, including partially loaded resources when preparation fails.
- Do not populate, replace, or consume the WinUI preview-resource cache.

### Renderer contract

- Allow a render caller to supply the image-resource resolver used for that operation.
- Preserve the existing renderer entry point for WinUI preview and print callers.
- Throw an explicit exception if an `ImageDrawable` has no resource in the active rendering context.
- Continue applying image effects and offsets using the resolved image.

### Failure behavior

- Missing, unreadable, invalid, or unsupported image files must fault the export task.
- Existing view-model command boundaries remain responsible for presenting or recovering from export failures.
- No file or clipboard output may be reported as successful after a required base image was omitted.

## Test plan

- Render a colored source image through `Win2DImageCanvasExporter` without constructing `ImageCanvas` or calling preview preparation.
- Decode the exported stream and verify that it contains the source raster.
- Verify the drawable still has no UI-prepared resource after export, proving the exporter used its own scope.
- Export an `ImageDrawable` whose file does not exist and verify the operation fails explicitly.
- Run the infrastructure edit test project and all non-UI tests.
- Build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Export of an image drawable succeeds without an `ImageCanvas` instance or preview resource.
- [x] Export loads the drawable's current `ImageFile` on the export device.
- [x] Missing image content fails the operation instead of producing annotation-only output.
- [x] Export resources have an operation-bounded lifetime and do not mutate UI preview state.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

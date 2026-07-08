# PRD: Windows AI Image Super Resolution

## Summary

GitHub issue: TBD

Implementation status: initial implementation is in place. The app has packaging support, service contracts, a Windows `ImageScaler` implementation, preparation consent UI, image edit command bar integration, per-session caching, variant switching, export integration through the existing renderer, and focused automated tests. Remaining work is manual validation on supported and unsupported Windows AI hardware.

CaptureTool should add a Windows AI Image Super Resolution toggle to the image edit command bar. When the user turns the toggle on for a loaded image, the app checks Windows AI feature availability, prepares the model if the user consents and preparation is required, generates a super-resolution version from the original image once, and displays that generated image in place of the original. Turning the toggle off restores the original image. Turning it back on reuses the generated image instead of processing the original again.

MVP product decision: the toggle creates a 2x super-resolution image, capped by the Windows AI `ImageScaler.MaxSupportedScaleFactor` and the documented 8x maximum. Unsupported systems keep the command disabled. Generated files are written to the app temporary folder and are reused only for the active image edit session.

## Source Notes

- Microsoft Image Super Resolution docs: https://learn.microsoft.com/en-us/windows/ai/apis/image-super-resolution
- Microsoft AI Imaging overview: https://learn.microsoft.com/en-us/windows/ai/apis/imaging
- Microsoft Windows AI API get-started and runtime readiness guidance: https://learn.microsoft.com/en-us/windows/ai/apis/get-started
- `ImageScaler` API reference: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.windows.ai.imaging.imagescaler

Important API constraints from the docs:

- Image Super Resolution uses `ImageScaler` to sharpen and scale images.
- Scaling is capped at 8x the original dimensions, and `ImageScaler.MaxSupportedScaleFactor` should also be respected.
- The app must call `GetReadyState` before using the model.
- `NotSupportedOnCurrentSystem` means the feature must not call `EnsureReadyAsync`.
- `NotReady` or `EnsureNeeded` means the app should ask for user consent before calling `EnsureReadyAsync`.
- Image Super Resolution is currently available on Copilot+ PCs with NPUs, not GPU or CPU.
- Windows AI imaging APIs require MSIX packaging with the `systemAIModels` capability and a `MaxVersionTested` value of `10.0.26226.0` or later. The implementation uses a `Windows.Universal` target device family at that version and opts out of WinAppSDK's generated default target-family item so the build output does not add a stale `Windows.Desktop` entry at the project target SDK version.

## Problem

CaptureTool can capture, crop, annotate, save, copy, print, and share screenshots, but it cannot improve the resolution or clarity of a low-resolution source image. Users who capture a small UI region, an older image, or a compressed screenshot must leave the app to upscale it.

The feature should feel like a natural part of the existing image edit workflow: a simple command bar toggle that swaps the base image while leaving the rest of the edit experience intact.

## Goals

- Add an `ImageEditPage` command bar toggle for Image Super Resolution.
- Generate the super-resolution image from the original loaded image, not from already rendered annotations, crop, rotation, or chroma key output.
- Generate once per loaded image and reuse the generated result for subsequent toggles.
- Keep crop, orientation, zoom, annotations, save, copy, print, and share behavior coherent when switching between original and super-resolution image variants.
- Use local Windows AI APIs only. No cloud fallback in the MVP.
- Handle unsupported hardware, missing models, model preparation failures, cancellation, and page disposal without corrupting the edit session.
- Preserve existing image edit behavior when the feature is unavailable or off.

## Non-Goals

- A scale-factor picker or before/after comparison slider.
- Batch upscaling recent captures.
- Upscaling exported annotations or the final rendered composition.
- Cloud AI fallback.
- Non-Windows implementation.
- Persisting generated super-resolution files across app launches.
- Full C2PA Content Credentials metadata in the MVP, though the UX must clearly show when the displayed image is AI-enhanced.

## User Stories

- As a user, I can turn on Super Resolution from the image edit command bar.
- As a user, I see a loading state while the app generates the super-resolution image.
- As a user on unsupported hardware, I do not get a broken control or failed model call.
- As a user whose system needs model preparation, I can consent before the app starts the Windows AI preparation flow.
- As a user, I can toggle back to the original image after viewing the super-resolution image.
- As a user, turning Super Resolution on again for the same loaded image is instant because the generated image is cached.
- As a user, save, copy, print, and share use the image variant I am currently viewing.
- As a user, my existing crop, orientation, annotations, and zoom do not disappear when I switch variants.

## Current Architecture

### Presentation

- `ImageEditPage.xaml` defines a centered `CommandBar` with edit toggles and export actions.
- `ImageEditPageViewModel` loads an `ImageFile`, creates an `ImageEditSession`, and adds a single base `ImageDrawable`.
- Crop, shape, text, chroma key, orientation, undo/redo, zoom, save, copy, print, and share all flow through `ImageEditPageViewModel`.
- `ImageCanvas` renders the current `Drawables` collection and uses `ImageSize`, `CropRect`, and `Orientation` from the view model.
- Existing mode toggles are mutually exclusive. Super Resolution should not be an edit mode and should not participate in crop/shapes/text/chroma key exclusivity.

### Rendering

- `ImageDrawable` stores an `ImageFile`, offset, size, and optional image effect.
- `Win2DImageCanvasRenderer.PrepareAsync` loads image bytes from `ImageDrawable.File.FilePath` into a `CanvasBitmap`.
- `Win2DImageCanvasExporter` renders the current drawables to PNG for save, copy, and share.
- This means the cleanest MVP display/export path is to swap the base `ImageDrawable.File` between the original and generated super-resolution `ImageFile`, then invalidate the canvas.

### Windows Packaging

- `CaptureTool.Presentation.Windows.WinUI` is already MSIX-enabled.
- `Package.appxmanifest` declares the `systemAIModels` capability and uses `MaxVersionTested="10.0.26226.0"` for Windows AI support.
- The WinUI and Windows edit infrastructure projects already reference `Microsoft.WindowsAppSDK`.
- `Directory.Packages.props` currently pins `Microsoft.WindowsAppSDK` to `2.2.0`; implementation should verify this version exposes the required Windows AI imaging APIs for the target SDK.

## Requirements

### Functional Requirements

1. The image edit command bar includes a Super Resolution `AppBarToggleButton`.
2. The toggle is disabled until an image has finished loading.
3. The view model exposes feature availability so the UI can hide or disable the toggle on unsupported systems.
4. On first toggle-on for a loaded image, the app checks Windows AI readiness before model use.
5. If readiness is `Ready`, generation starts immediately.
6. If readiness is `NotReady` or `EnsureNeeded`, the app asks for explicit user consent before calling `EnsureReadyAsync`.
7. If readiness is `NotSupportedOnCurrentSystem`, the app does not call `EnsureReadyAsync` and leaves the original image active.
8. The generated image is based on the original loaded image file.
9. The generated image is cached for the current edit session and reused for later toggle-on actions.
10. Toggle-off restores the original image without deleting the cached generated image.
11. Save, copy, print, and share render the active image variant.
12. The command does not enter the undo/redo stack.
13. The unsaved-change guard treats an active generated image as an unsaved content change; toggling back to the original restores the previous dirty state.
14. Existing user edits remain visible and spatially aligned when switching between variants.
15. Generation failures revert the toggle to off, keep the original image visible, and surface a user-readable failure.
16. Cancellation or page disposal stops updating the disposed view model and does not leave the command stuck in a loading state.

### Technical Requirements

1. Add an application abstraction, likely under `CaptureTool.Application.Abstractions.Edit.Image.SuperResolution`.
2. The abstraction should separate availability, readiness/preparation, and image generation. A candidate shape:
   - `ImageSuperResolutionReadyState`
   - `ImageSuperResolutionPreparationResult`
   - `ImageSuperResolutionRequest`
   - `ImageSuperResolutionResult`
   - `IImageSuperResolutionService`
3. Add a Windows implementation in `CaptureTool.Infrastructure.Edit.Windows`.
4. The Windows implementation uses `ImageScaler.GetReadyState`, `ImageScaler.EnsureReadyAsync`, `ImageScaler.CreateAsync`, and `ScaleSoftwareBitmap`.
5. The implementation decodes the original image into a `SoftwareBitmap`, scales to the requested target size, and encodes the generated image as PNG in the app temporary folder.
6. Target dimensions are calculated from original dimensions and the selected scale factor, capped by `ImageScaler.MaxSupportedScaleFactor` and the documented 8x limit.
7. Large images must be guarded by a maximum output pixel count or memory budget before allocating the target bitmap.
8. The generated file path should include enough entropy to avoid collisions and should live under the app temporary folder.
9. The view model owns per-load variant state:
   - original `ImageFile`
   - original size
   - generated `ImageFile`
   - generated size
   - active variant
   - generation status
10. Switching variants must update the base `ImageDrawable`, `ImageSize`, `CropRect`, drawables, and canvas invalidation together.
11. If the generated image uses 2x dimensions, crop rectangles and non-image drawable coordinates must scale by the active/original ratio so visual placement is preserved.
12. Coordinate scaling must avoid cumulative drift by deriving transforms from original-space state, not repeatedly scaling already-scaled values.
13. The original image file is never overwritten.
14. Concurrent toggle requests must deduplicate generation and prevent two model calls for the same loaded image.
15. Readiness and generation should run asynchronously without blocking the UI thread.
16. Add manifest support:
   - add `xmlns:systemai="http://schemas.microsoft.com/appx/manifest/systemai/windows10"`
   - include `systemai` in `IgnorableNamespaces`
   - add `<systemai:Capability Name="systemAIModels" />`
   - update `MaxVersionTested` to at least `10.0.26226.0`
   - prevent WinAppSDK single-project packaging from injecting a lower-version default target device family
17. Add project settings so packaging does not overwrite the manifest versions:
   - `AppxOSMinVersionReplaceManifestVersion=false`
   - `AppxOSMaxVersionTestedReplaceManifestVersion=false`
18. Verify Native AOT and trimming still work with the Windows Runtime types used by the implementation.

### UX Requirements

1. Place Super Resolution near other image-transform commands in the command bar, after Chroma Key and before Crop/Shapes/Text, or after Flip before Undo/Redo if the command feels more like image transformation than editing mode.
2. Use a toggle button because the feature is a variant display state, not a one-shot command.
3. The button has localized label, tooltip, and access key strings.
4. While generating, the button is disabled and visually communicates progress.
5. If model preparation is required, show a confirmation dialog before calling `EnsureReadyAsync`.
6. The dialog explains that Windows may prepare or download local AI components through Windows Update.
7. Unsupported hardware should result in a disabled or hidden button, not a repeatable error path.
8. When the generated variant is active, the toggle remains checked and the UI clearly indicates the image is AI-enhanced.
9. If generation fails, show a concise error and leave the original image visible.
10. The feature must be keyboard accessible and screen-reader discoverable.

## Proposed Implementation

### Phase 1: Service and Packaging

- Add `IImageSuperResolutionService` and result models in application abstractions.
- Implement `WindowsImageSuperResolutionService` in `CaptureTool.Infrastructure.Edit.Windows`.
- Register the service in `WindowsEditInfrastructureServiceCollectionExtensions`.
- Add Windows AI manifest capability and max-version changes.
- Add resource strings for the command and any consent/error UI.
- Add a no-op or unavailable fallback only if needed for tests or non-Windows composition.

### Phase 2: View Model Integration

- Add properties to `ImageEditPageViewModel`:
  - `IsSuperResolutionAvailable`
  - `IsSuperResolutionActive`
  - `IsSuperResolutionGenerating`
  - `CanToggleSuperResolution`
- Add `ToggleSuperResolutionCommand`.
- On first toggle-on, call readiness/preparation/generation, cache the result, swap active variant, and invalidate the canvas.
- On later toggle-on, swap to the cached result without calling the service.
- On toggle-off, restore the original variant.
- Track unsaved state so active super-resolution output participates in save/leave behavior.
- Ensure `Dispose` cancels pending work and resets variant state.

### Phase 3: Image Geometry and Canvas

- Add a safe way to swap the base image file and image size in `ImageEditSession`.
- If using 2x output, preserve visual alignment by mapping edit state between original coordinate space and active variant coordinate space.
- Avoid cumulative coordinate drift by storing canonical edit state and deriving active coordinates from it.
- Force image resource reload on the canvas when the base image file changes.
- Confirm crop, orientation, chroma key, shape, text, undo/redo, zoom, save, copy, print, and share still operate on the active variant.

### Phase 4: Tests and Manual Validation

- Add presentation tests with a fake super-resolution service:
  - unsupported availability disables the command
  - first toggle generates once
  - second toggle-on reuses the cached image
  - failures revert to original
  - active variant is included in save/share render inputs
  - dirty state is correct for active and inactive generated variants
- Add application or infrastructure tests for target-size calculation and cap behavior.
- Add manifest regression checks if the project has an appropriate test pattern.
- Manually validate on a Copilot+ PC with a supported NPU.
- Manually validate on an unsupported Windows machine.
- Manually validate cancellation by navigating away during generation.

## Work Items

These items are intentionally sized so each can land as a meaningful PR with focused validation. Items can be tracked as separate GitHub issues once the scale policy is decided.

### 1. Confirm Product and Geometry Policy

Goal: decide the MVP behavior before implementation starts.

Scope:

- MVP uses fixed 2x upscaling.
- Unsupported systems show a disabled command.
- Generated files live in the app temporary folder and are reused only for the current edit session.
- The feature is not behind a feature flag.

Deliverables:

- Update this PRD with final decisions.
- Create implementation issues from any follow-up work.

Validation:

- Product and engineering agree on the scale policy and unsupported-hardware UX.
- Implementation follows those decisions.

Status: complete.

Dependencies: none.

### 2. Add Windows AI Packaging Support

Goal: make the packaged app eligible to use Windows AI imaging APIs.

Scope:

- Update `Package.appxmanifest` with the `systemai` namespace.
- Add the `systemAIModels` capability.
- Update `MaxVersionTested` to `10.0.26226.0` or later.
- Prevent WinAppSDK single-project packaging from injecting a lower-version default target device family.
- Add project settings that prevent packaging from overwriting manifest min/max version values.
- Confirm the pinned Windows App SDK package exposes the required imaging APIs.

Deliverables:

- Manifest and project file updates.
- A small validation note in the PR describing package build behavior.

Validation:

- WinUI project builds.
- MSIX packaging still succeeds.
- Existing app capabilities remain unchanged except for Windows AI model access.

Dependencies: item 1 can be in progress but is not strictly required.

Status: complete.

### 3. Add Super Resolution Application Contracts

Goal: define the app-facing abstraction without taking a dependency on Windows runtime details in presentation code.

Scope:

- Add super-resolution request, result, ready-state, and preparation-result models.
- Add `IImageSuperResolutionService`.
- Include target-size or scale-factor inputs and generated-file outputs.
- Include enough error/status information for user-facing messages.

Deliverables:

- New contracts under `CaptureTool.Application.Abstractions.Edit.Image.SuperResolution`.
- Unit tests for pure target-size calculation if that logic lives with the contracts.

Validation:

- Solution builds.
- Contracts support ready, preparation-needed, unsupported, cancelled, and failed outcomes.

Dependencies: item 1.

Status: complete.

### 4. Implement Windows ImageScaler Service

Goal: provide the Windows implementation that checks readiness, prepares the model, and generates an image.

Scope:

- Add `WindowsImageSuperResolutionService`.
- Use `ImageScaler.GetReadyState`, `EnsureReadyAsync`, `CreateAsync`, and `ScaleSoftwareBitmap`.
- Decode the source image into `SoftwareBitmap`.
- Encode the generated result as PNG in the app temporary folder.
- Cap scale by `MaxSupportedScaleFactor`, the documented 8x limit, and an app memory or pixel budget.
- Register the service in Windows edit infrastructure DI.

Deliverables:

- Windows service implementation.
- Service registration.
- Tests for target-size cap behavior and unsupported/error mapping where testable without supported hardware.

Validation:

- Solution builds.
- Unsupported systems return unsupported status without calling model preparation.
- Large target sizes fail before unsafe allocation.

Dependencies: items 2 and 3.

Status: complete.

### 5. Add Preparation Consent UI Contract

Goal: give the view model a clean way to ask the user before model preparation.

Scope:

- Add an interface for image AI preparation consent, similar in spirit to existing edit-session confirmation services.
- Implement the WinUI dialog.
- Localize dialog title, body, confirm, and cancel strings.
- Keep the dialog separate from the Windows service so service calls remain testable.

Deliverables:

- Application abstraction for consent.
- WinUI implementation and DI registration.
- Localized resources.

Validation:

- Dialog appears only when readiness indicates preparation is needed.
- Cancelling leaves the original image active.

Dependencies: item 3.

Status: complete.

### 6. Add View Model State and Toggle Command

Goal: wire the feature into `ImageEditPageViewModel` without changing the canvas yet.

Scope:

- Add availability, active, generating, and can-toggle properties.
- Add `ToggleSuperResolutionCommand`.
- Load initial availability after an image loads.
- Call consent and service on first toggle-on.
- Cache the generated result per loaded image.
- Reuse the cached result for later toggle-on actions.
- Revert the toggle on failure or cancellation.
- Reset state on `Dispose`.

Deliverables:

- View model changes.
- Presentation tests using fake service and fake consent.

Validation:

- First toggle generates once.
- Second toggle-on reuses cache.
- Unsupported state disables or hides the command according to item 1.
- Failures and cancellation keep the original active.

Dependencies: items 3, 4, and 5.

Status: complete.

### 7. Swap Image Variant and Preserve Edit Geometry

Goal: make the active image variant actually appear on the canvas while preserving user edits.

Scope:

- Add a safe way to replace the base `ImageDrawable.File` and active image size.
- If MVP uses 2x output, map crop and drawable coordinates between original and active coordinate spaces.
- Avoid cumulative scaling drift by keeping canonical original-space edit state.
- Force canvas image resource reload after the base image file changes.
- Keep undo/redo behavior scoped to user edits, not variant toggles.

Deliverables:

- Image edit session and view model updates.
- Tests for coordinate mapping, cache switching, and undo/redo behavior.

Validation:

- Toggling on shows the generated image.
- Toggling off restores the original.
- Existing shapes, text, crop, orientation, and chroma key remain visually aligned.

Dependencies: item 6.

Status: complete.

### 8. Add Command Bar UI and Localized Strings

Goal: expose the feature in the image edit command bar.

Scope:

- Add the Super Resolution `AppBarToggleButton`.
- Bind checked, enabled, command, and progress-related state.
- Add label, tooltip, and access-key resources.
- Add a clear active AI-enhanced indication if the command itself is not enough.
- Ensure keyboard and screen-reader accessibility.

Deliverables:

- XAML updates.
- Resource updates for all existing localization files, or a documented fallback strategy.

Validation:

- Button is unavailable until image load completes.
- Button reflects active and generating states.
- UI remains coherent at narrow and wide window sizes.

Dependencies: item 6. Item 7 can land before or after this if the button is hidden behind a temporary disabled path.

Status: complete.

### 9. Integrate Export, Dirty State, and Navigation Guard

Goal: ensure the active variant participates correctly in save/copy/print/share and unsaved-change prompts.

Scope:

- Confirm export paths render the active base image variant.
- Treat active generated output as an unsaved content change.
- Restore previous dirty state when toggling back to the original.
- Confirm save clears the dirty state for the active output.
- Confirm navigation confirmation behaves correctly with active generated output.

Deliverables:

- View model and test updates.
- Any required adjustment to save/share tests.

Validation:

- Save, copy, print, and share use the displayed variant.
- Unsaved-change prompts appear only when expected.
- Existing image edit tests pass.

Dependencies: items 7 and 8.

Status: complete.

### 10. Manual Hardware Validation and Release Gate

Goal: validate real Windows AI behavior before enabling broadly.

Scope:

- Test on a Copilot+ PC with supported NPU.
- Test on unsupported Windows hardware.
- Test first-run preparation path if a machine reports preparation needed.
- Test cancellation by navigating away during generation.
- Test representative small, normal, and large images.
- Confirm app packaging and Store-related validation still pass.

Deliverables:

- Manual validation notes attached to the release issue or PR.
- Final checklist updates in this PRD.
- Decision on feature flag default.

Validation:

- Supported hardware generates and displays output.
- Unsupported hardware does not call preparation or generation.
- No blocking package, startup, or export regressions.

Dependencies: items 2 through 9.

Status: pending manual validation.

## Acceptance Criteria

- A loaded image edit page shows a Super Resolution command bar toggle on supported systems.
- Toggling on generates a super-resolution image from the original image and displays it.
- The generated result is reused when toggling off and back on for the same loaded image.
- Toggling off restores the original image.
- Save, copy, print, and share use whichever image variant is currently displayed.
- Existing crop, orientation, annotations, zoom, and chroma key behavior remains coherent after toggling variants.
- Unsupported systems do not attempt to prepare or invoke the model.
- Model preparation requires user consent when readiness indicates preparation is needed.
- Large images fail gracefully before unsafe allocation.
- Existing image edit tests pass.
- New tests cover command behavior, caching, failure handling, and export integration.

## Open Questions

- Should the MVP remain fixed at 2x, or should a future version add a scale picker?
- Should the generated super-resolution image be saved as PNG only, or should it preserve the original file format when possible?
- Should the disabled unsupported-state command include a richer explanation than the tooltip/status text?
- Should the generated temp file be deleted when the page unloads, or left for the existing temporary-file cleanup path as implemented?
- Should active AI-enhanced exports include Content Credentials metadata in a later phase?
- Should this feature be behind a feature flag before broad release if manual hardware validation finds compatibility issues?

## Risks

- Windows AI imaging API availability may vary by Windows App SDK version, Windows build, and hardware.
- Image Super Resolution is NPU-only today, which limits manual and automated test coverage.
- Large images can create high memory pressure after 2x scaling.
- Mapping crop and annotations between original and generated coordinate spaces can introduce rounding drift if not designed carefully.
- Manifest capability changes may affect Store certification or package validation.
- Native AOT and trimming may require extra care for Windows Runtime interop.
- CI will likely not have supported Windows AI hardware, so service behavior needs test doubles and explicit manual validation.

## Tracking Checklist

- [x] Confirm scale policy: 2x upscale.
- [x] Add Windows AI manifest capability and max-version settings.
- [x] Add image super-resolution abstraction.
- [x] Implement Windows `ImageScaler` service.
- [x] Register the service in DI.
- [x] Add command bar toggle and localized strings.
- [x] Add view model state, command, caching, and cancellation.
- [x] Add image variant swap and coordinate mapping.
- [x] Ensure save/copy/print/share use the active variant.
- [x] Add presentation tests.
- [x] Add target-size and cap tests.
- [ ] Manually validate supported hardware.
- [ ] Manually validate unsupported hardware.

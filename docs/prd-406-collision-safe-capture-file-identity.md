# PRD: Collision-Safe Capture File Identity

## Summary

GitHub issue: https://github.com/shanebweaver/CaptureTool/issues/406

Status: approved for implementation in this change.

CaptureTool must assign every newly captured image, video, and audio asset a collision-resistant filename and must never overwrite an existing auto-saved capture as a collision policy. The filename remains human-readable, but uniqueness no longer depends on a partial timestamp.

## Problem

The image, video, and audio filename generators currently use this pattern:

```text
Capture_{yyyy-MM-dd}_{FFFFF}.extension
```

`FFFFF` represents only the fractional portion of a second. It omits hour, minute, and whole second, so captures taken on the same day can generate the same filename. Auto-save then calls `CopyFile` with overwrite enabled, allowing a later capture to replace an earlier file silently.

This affects temporary capture identity and persistent auto-save destinations for images, videos, and audio recordings.

## Goals

- Generate collision-resistant filenames for all three captured media types.
- Retain a readable local timestamp in each filename for sorting and diagnosis.
- Prevent auto-save from overwriting an existing destination under every circumstance.
- Keep the existing per-media filename generator and post-processor boundaries stable.
- Add regression coverage for equal clock values, parallel generation, extensions, and no-overwrite copy behavior.

## Non-Goals

- Renaming or migrating captures that already exist.
- Changing user-selected Save As overwrite behavior.
- Changing recent-capture retention or temporary-file cleanup policy.
- Changing capture contents, codecs, or metadata.
- Guaranteeing that auto-save succeeds when the destination is unavailable, full, or rejects writes.

## User Stories

- As a user taking several captures quickly, I do not lose an earlier capture because a later capture reused its name.
- As a user running captures at the same clock value, every asset still receives a distinct path.
- As a user browsing an auto-save folder, filenames remain chronologically understandable.
- As a user with an existing file at a generated destination, CaptureTool preserves that file rather than overwriting it.

## Product Requirements

### Filename contract

1. Image, video, and audio capture filenames must use the canonical pattern:

   ```text
   Capture_yyyy-MM-dd_HHmmss_fffffff_<32 lowercase hexadecimal characters>.extension
   ```

2. The timestamp must come from the existing `IClock` dependency and use local-time semantics consistent with the current implementation.
3. The suffix must be a newly generated GUID formatted with `N`, making identity independent of clock precision, process scheduling, and capture concurrency.
4. Extensions must remain `.png`, `.mp4`, and `.wav` for image, video, and audio captures respectively.
5. All media generators must use one shared formatting implementation so the contract cannot drift between capture types.

### Auto-save safety

1. Capture post-processors must copy to auto-save destinations with overwrite disabled.
2. A destination collision must never modify the existing destination.
3. A destination collision must generate a new candidate and retry, up to a bounded maximum of 10 attempts.
4. If an auto-save copy fails for a reason other than collision, the current behavior remains: log/telemetry records the output failure, the temporary capture remains available, and recent-capture path replacement does not occur.
5. Explicit Save As operations are outside this rule because the file picker and user intent own their overwrite semantics.

### Temporary capture safety

1. A temporary destination must be reserved atomically with create-new semantics before an image or recorder adapter receives its path.
2. A temporary-path collision must generate a new candidate and retry, up to a bounded maximum of 10 attempts.
3. A failed image save or recorder start must remove the reservation without masking the original failure.
4. A successfully started capture owns the reserved file and continues through the existing lifecycle.

## Design Decision

Use a full timestamp for readability, a GUID suffix for collision resistance across threads and processes, and a shared allocator as the hard data-safety boundary. The allocator atomically reserves temporary paths with `FileMode.CreateNew`, copies auto-save output with `overwrite: false`, and retries confirmed collisions up to 10 times.

The alternatives are weaker:

- A fuller timestamp alone still admits collisions at the clock's resolution and under injected/frozen clocks.
- An in-process sequence does not protect across restarts or processes and introduces singleton lifecycle state.
- Checking `FileExists` before an overwrite-enabled copy has a time-of-check/time-of-use race.
- Relying on GUID probability alone does not enforce the no-overwrite invariant and leaves behavior to individual capture adapters.

## Acceptance Criteria

- Two calls made with the same `IClock.Now` value produce different filenames for each media type.
- Parallel filename generation produces no duplicates.
- Each filename contains the complete expected timestamp and a 32-character GUID suffix.
- Image filenames end in `.png`, video filenames in `.mp4`, and audio filenames in `.wav`.
- Image, video, and audio auto-save calls use `overwrite: false`.
- Temporary image, video, and audio destinations are reserved with create-new semantics.
- Confirmed collisions retry with a new candidate and stop after 10 attempts.
- A pre-existing auto-save destination is never overwritten.
- A pre-existing temporary destination is never overwritten by allocation.
- Recent-capture path replacement occurs only after a successful copy.
- Existing capture and application tests remain green.

## Validation Plan

### Automated

- Add focused unit tests for the canonical filename format and same-clock uniqueness across all media generators.
- Add a parallel-generation test over the shared filename implementation.
- Add deterministic allocator tests for temporary reservation, auto-save copy, collision retry, non-collision failure, and bounded exhaustion.
- Verify failed image/recorder startup cleans its reserved temporary file.
- Verify the Windows image adapter replaces an app-owned reservation with encoded image data.
- Run the full `CaptureTool.Application.Tests` project.
- Run the infrastructure filesystem tests to retain coverage of create-new copy semantics.

### Manual

No UI validation is required for this isolated naming change. If a packaged smoke test is performed, take several rapid captures of each enabled type and confirm that the resulting temporary/auto-save files are distinct and chronologically readable.

## Risks and Mitigations

- **Longer filenames:** The additional timestamp fields and GUID remain far below Windows path-component limits.
- **External filename parsing:** No supported contract documents the old pattern. The `Capture_` prefix and media extensions remain stable.
- **Random collision:** A GUID collision is operationally negligible, but create-new/copy-no-overwrite semantics and bounded retry enforce the invariant independently of probability.
- **Adapter writes to a reserved file:** The reservation is an app-owned empty file. Existing image and CaptureKit adapters already accept a destination path they create or replace; startup failure cleanup removes a rejected reservation.
- **Collision retry exhaustion:** The operation fails without modifying the pre-existing files, and normal capture failure handling remains in control.

## Rollout

The change requires no migration or feature flag. New captures use the new identity format immediately; existing files and recent-capture entries remain valid.

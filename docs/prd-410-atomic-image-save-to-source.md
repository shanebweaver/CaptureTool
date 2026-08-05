# PRD: Atomic Image Save to Source

- Issue: [#410](https://github.com/shanebweaver/CaptureTool/issues/410)
- Architecture finding: `ARCH-05`
- Severity: High
- Status: Implemented
- Affected features: `APP-05`, `APP-06`, `IMG-01`, `IMG-15`

## Summary

Image Save to Source must commit one rendered image without overwriting the live editor base. When an editor has a distinct persistent source, the persistent file is the only save destination and the temporary working copy remains immutable. When the working file is itself the source, the save is committed atomically and the editor is rebased to that flattened image before editing continues.

## Problem

`ImageEditPageViewModel.SaveToSourceAsync` currently renders independently to the working file and then to the associated persistent file. The first write mutates the file referenced by the active base `ImageDrawable`, while annotations, crop, orientation, and undo history remain live. Win2D normally hides the mismatch behind its cached bitmap, but resource recreation reloads the flattened file and applies the still-live edits again.

The two writes also form a partial commit. If the persistent write fails, the working file has already changed even though the operation reports failure and retains a dirty retry state.

## Goals

1. Keep a distinct editor working file immutable for the lifetime of its edit session.
2. Ensure a failed Save to Source leaves every source destination unchanged.
3. Render the current edit state once per save operation.
4. Preserve the current session and undo history when saving to a distinct persistent source.
5. Rebase the session safely when the working file is the only source that can be saved.
6. Keep Save As behavior and existing image format selection intact.

## Non-goals

- Redesigning Save As, clipboard, share, or print output.
- Adding a cross-filesystem transaction for unrelated destinations.
- Persisting editable annotation metadata alongside the flattened image.
- Changing capture auto-save or recent-capture identity behavior.
- Changing the confirmation UI introduced for unsaved edit sessions.

## User experience

- Saving an opened or auto-saved image updates its persistent source while the editor continues to use its unchanged temporary working copy.
- Saving a temporary-only capture updates that capture atomically, then presents the saved pixels as a clean new editing baseline.
- A failed save keeps the editor dirty and leaves the previous source bytes available for retry.
- Continuing to edit after a successful save cannot duplicate crop, orientation, chroma key, or annotations after canvas resource recreation.

## Functional requirements

### Destination selection

1. If `PersistentFilePath` identifies a path distinct from `FilePath`, Save to Source writes only `PersistentFilePath`.
2. The working `FilePath` remains unchanged and continues to back the live base drawable.
3. If no distinct persistent path exists, Save to Source writes `FilePath` and treats it as the sole source.

### Atomic output

1. The exporter renders the drawable snapshot once to an in-memory encoded image.
2. Encoded bytes are written to a uniquely named temporary sibling of the destination.
3. The destination is replaced only after the temporary file has been fully written and accepted by the platform file-update API.
4. A staging or replacement failure removes the temporary sibling on a best-effort basis and leaves the previous destination intact.
5. The temporary sibling retains the destination extension so image format selection is unchanged.

### Session consistency

1. Saving to a distinct persistent source does not replace the base drawable, geometry, annotations, or undo history.
2. Saving to the sole working source rebases the editor to the saved flattened file.
3. Rebase creates one base image drawable, resets crop and orientation to the saved dimensions, clears annotation effects and undo/redo history, and reloads canvas resources.
4. Only a fully successful commit marks the session clean.

## Reliability requirements

- The existing destination must not be truncated before the staged file is ready.
- A failed persistent save must not write the working file.
- Temporary-file cleanup must not mask the original save failure.
- Source path comparison remains full-path and case-insensitive on Windows.
- Cancellation observed before commit must retain the dirty session.

## Test plan

### Presentation regression tests

- A distinct persistent source is the only exporter destination; the working path is never written.
- A persistent-source failure keeps unsaved changes and never attempts a working-file write.
- A successful persistent save retains annotations and undo history over the immutable working base.
- A temporary-only source save rebases to a single base drawable with default orientation/crop and cleared undo/redo state.
- A second output after rebase contains only the new baseline plus edits added after the save.

### Exporter validation

- The existing file remains intact when staging fails.
- Successful staging replaces the destination and removes the sibling temporary file.
- Temporary-file cleanup is best effort and does not alter the reported failure.

### Repository validation

- Run all non-UI test projects.
- Build the WinUI x64 Debug project.

## Acceptance criteria

- [x] A distinct working image is never overwritten by Save to Source.
- [x] The persistent source is replaced only after a complete staged write.
- [x] Failure leaves the previous working and persistent sources recoverable.
- [x] Resource recreation after save cannot reapply edits already flattened into the source.
- [x] Temporary-only sources are rebased to a clean, single-image session after save.
- [x] Save As behavior remains unchanged.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No migration or feature flag is required. The new save behavior applies immediately to image edit sessions and does not change stored file formats.

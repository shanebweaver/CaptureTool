# PRD: Video Dirty State Relative to Last Save

- Issue: [#417](https://github.com/shanebweaver/CaptureTool/issues/417)
- Architecture finding: `ARCH-12`
- Severity: Medium
- Status: Implemented
- Affected features: `APP-07`, `VID-02`, `VID-03`

## Summary

The video editor must derive `HasUnsavedChanges` by comparing the current edit state with the state captured at load or the last successful save. The saved state consists of the normalized trim range and active video variant (original or super-resolution).

## Problem

The current dirty-state calculation is relative to the original video. A successful save sets `HasUnsavedChanges` to false, but later trim changes recompute it as `IsTrimmed || IsVideoSuperResolutionActive`. This loses the saved baseline.

After saving a trimmed range, restoring the full original range incorrectly reports a clean session even though the visible output differs from the last save. Conversely, returning to the saved trimmed range can remain dirty. Super-resolution tracking has the same problem because it considers generated-file existence or whether the enhanced variant is currently active rather than which variant was last saved.

## Goals

1. Make dirty state relative to the last successful save.
2. Track trim and original/super-resolution variant changes in one value-semantic snapshot.
3. Treat returning exactly to the saved state as clean.
4. Treat switching away from the saved state as dirty, including switching back to the full original range.
5. Preserve the saved baseline when save fails or is cancelled.
6. Keep asynchronous video-duration discovery from marking a newly loaded full-range video dirty.

## Non-goals

- Changing video file output, trimming, or super-resolution generation.
- Persisting edit-session metadata across application restarts.
- Adding undo/redo to the video editor.
- Changing copy or external-editor behavior.
- Changing the existing trim comparison tolerance.

## Functional requirements

### Edit snapshot

1. Add a value-semantic snapshot containing trim start, trim end, and active video variant.
2. Normalize a full-duration range to no trim values so it remains equivalent as duration metadata is first discovered.
3. Compare trim values using the existing `TrimComparisonToleranceSeconds` tolerance.
4. Represent original and super-resolution variants explicitly; generated-file existence is not edit state.

### Baseline lifecycle

1. Capture the baseline after loading a video in its original, full-range state.
2. Replace the baseline only after `SaveVideoFileResponse.Saved` is true.
3. Do not replace the baseline after a failed or cancelled save.
4. Reset the baseline when a different video is loaded.

### Dirty-state derivation

Recompute dirty state after:

- trim start or end changes;
- trim range reset caused by duration changes;
- switching to the original variant;
- generating, reusing, or switching to the super-resolution variant;
- a successful save.

## User experience

- Saving trim `2s-8s`, then returning to the full `0s-10s` range shows an unsaved-work warning.
- Returning from another range to the saved `2s-8s` range clears the warning.
- Saving the enhanced variant makes the original variant dirty; switching back to enhanced clears the warning.
- Saving the original variant makes enhanced dirty; switching back to original clears the warning.
- Failed saves leave the existing warning and baseline unchanged.

## Reliability requirements

- Initial media-duration loading must not create a false dirty state.
- Equivalent floating-point trim positions within the existing tolerance compare as equal.
- Cached super-resolution output must not make the original variant dirty unless the active variant differs from the saved baseline.
- Dirty-state updates remain synchronous with user-visible trim and variant properties.

## Test plan

Add focused presentation tests for:

- save trimmed range, then restore full range;
- save trimmed range, change it, then return to the saved range;
- save enhanced variant, switch to original, then return to enhanced;
- save original with enhanced output cached, switch to enhanced, then return to original;
- failed save retaining the previous baseline;
- initial duration discovery remaining clean.

Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Dirty state is derived from the last loaded or successfully saved edit snapshot.
- [x] Full-range and saved-range transitions report the correct dirty state.
- [x] Original and super-resolution saved variants report the correct dirty state.
- [x] Failed saves do not change the baseline.
- [x] Initial duration discovery remains clean.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No migration or feature flag is required. The corrected dirty-state calculation applies to video edit sessions immediately.

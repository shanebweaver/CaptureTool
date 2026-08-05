# PRD: Rebase image-edit history across resolution changes

- Issue: [#418](https://github.com/shanebweaver/CaptureTool/issues/418)
- Architecture finding: `ARCH-13`
- Severity: Medium
- Status: Implemented
- Affected features: `IMG-02`, `IMG-03`, `IMG-04`, `IMG-05`, `IMG-06`, `IMG-10`

## Summary

Image-edit history must preserve normalized edit geometry when the active image variant changes resolution. Each history entry will remember the image size where its command was recorded. Before undo or redo, the history will rebase geometry captured by that command from its recorded size to the session's current size.

This keeps super-resolution variant switching outside the undo stack while making existing crop and drawable commands safe to replay at either the original or enhanced resolution.

## Problem

`ImageEditSession.ResizeImage` scales the current crop rectangle and live drawables. Existing undo and redo entries still contain absolute rectangles, drawable states, and sometimes detached drawable instances from the resolution where the edit was recorded.

After switching from a 1x image to a 2x enhanced image, undoing an earlier crop or drawable modification applies the stored 1x values to the 2x canvas. A deleted or previously undone drawable can likewise be reinserted at the wrong size. Switching variants around undo and redo can therefore move, shrink, or mis-crop edits.

## Goals

1. Preserve normalized crop and drawable geometry across image-resolution changes.
2. Support edits recorded before and after a variant switch in the same undo/redo history.
3. Rebase both undo and redo entries without adding variant switches to user-visible history.
4. Cover crop plus rectangle, ellipse, line, arrow, and text modification state.
5. Cover drawable instances temporarily detached by add/delete undo state.
6. Preserve existing behavior for commands that do not capture resolution-dependent geometry.

## Non-goals

- Do not make super-resolution activation itself undoable.
- Do not redesign the image-edit history as full immutable session snapshots.
- Do not change super-resolution generation, caching, availability, or failure behavior.
- Do not change orientation, chroma-key, or image-file replacement semantics.
- Do not address lossy rounding from repeated arbitrary non-integral resize cycles.

## Functional requirements

### Resolution-aware history entries

- Store the current `ImageEditSession.ImageSize` with every executed command.
- Before undo or redo, compare the entry's recorded size with the current session size.
- Rebase resolution-dependent command state exactly once to the current size, then update the entry's recorded size.
- Leave command state unchanged when either size cannot produce valid scale factors.

### Geometry rebasing

- Scale both old and new crop rectangles for crop commands.
- Scale old and new shape states for drawable-modification commands.
- Use the same per-axis geometry and average stroke/font scaling rules as `ImageEditSession.ResizeImage`.
- Scale a drawable retained by an add/delete command only while it is detached from the live session; live drawables have already been scaled by `ResizeImage` and must not be scaled twice.
- Treat commands with no captured geometry as no-ops during rebasing.

## Reliability requirements

- Undo and redo ordering and availability must remain unchanged.
- New command execution must continue to clear the redo stack.
- Repeated undo/redo at one resolution must not accumulate additional scaling.
- A history containing a mixture of original-resolution and enhanced-resolution edits must replay each entry against the active resolution.

## Test plan

- Reproduce a pre-enhancement crop edit, resize to 2x, and verify undo/redo use 2x rectangles.
- Reproduce pre-enhancement modifications for rectangle, ellipse, line, arrow, and text drawables; resize to 2x; verify undo/redo preserve normalized geometry.
- Verify an undone add command rebases its detached drawable before redo.
- Verify a delete command rebases its detached drawable before undo.
- Verify entries created at different resolutions independently rebase to the current session size.
- Run all non-UI tests and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Pre-enhancement crop history replays with geometry normalized to the active image size.
- [x] Pre-enhancement drawable modification history replays with normalized geometry for every editable drawable type.
- [x] Detached add/delete drawables are restored at the active resolution without double-scaling live drawables.
- [x] History recorded on both sides of a resolution change remains ordered and reversible.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

# PRD: Protect Edit Sessions from Destructive Navigation

## Source

- GitHub issue: <https://github.com/shanebweaver/CaptureTool/issues/328>
- Title: Navigating to Settings or About page is destructive

## Problem

Users can lose in-progress image or video edits when they navigate away from an edit page, close the main window, or visit settings/about and later return. The app currently treats the edit page as disposable UI state, while the captured working file lives in the temp directory and export actions are labeled like durable saves.

## Goals

- Warn users before leaving a dirty edit session when that action would discard unsaved work.
- Let users save or discard before navigating away or closing.
- Add a setting that allows users to enable or disable destructive-navigation warnings.
- Rename export-only `Save` actions to `Save as`.

## Non-Goals

- Replace the temp-working-file model with a full project-file workflow.
- Add cloud sync, version history, or cross-device recovery.
- Add new editing features beyond persistence protection.

## Users

- Screenshot editors who annotate, crop, rotate, flip, or apply chroma key before sharing/exporting.
- Video editors who trim a captured video before export.
- Users who frequently open Settings/About mid-work and expect to return without accidental data loss.

## Functional Requirements

### Dirty Session Protection

- The app must detect when the current edit page has unsaved mutable state.
- Image edits are dirty after any undoable image edit operation changes the render result.
- Video edits are dirty when the trim range differs from the full source duration.
- Audio edit pages currently have no mutable edit operations and do not need dirty-session prompts.
- When warning is enabled and a dirty edit session exists, navigation away from the edit page must ask whether to save, discard, or cancel.
- Window close must use the same save/discard/cancel decision.
- Cancel must leave the user on the current edit page and preserve navigation history.
- Discard must proceed without exporting the current edits.
- Save must export the current result, then proceed only if the export succeeds or is not canceled by the user.

### Settings

- Add a setting to enable or disable warnings before discarding edit sessions.
- The setting must be visible in Settings and persisted through the existing settings service.
- Defaults:
  - destructive-navigation warning: enabled
- The app must not automatically persist edit changes to the original, temp, or captured output media while the user is actively editing.
- Edit changes are durable only when the user explicitly chooses `Save as`, or chooses save from the destructive-navigation confirmation flow.

### Save as Labeling

- Image, video, and audio edit export commands currently labeled `Save` must be labeled `Save as`.
- Context menu export entries must also use `Save as`.
- Keyboard accelerators and command behavior remain unchanged.

## Architecture

- Domain owns edit-state concepts and dirty-state rules where possible.
- Application owns use cases, settings, persistence ports, and guarded navigation orchestration.
- Infrastructure owns filesystem/media persistence implementations.
- Presentation owns dialogs, page bindings, and user-facing labels.
- Navigation guards must run before the navigation stack mutates.

## Task Breakdown

1. Add PRD and task contract.
2. Add edit-session dirty-state abstractions and implementations for image/video editors.
3. Add confirmation setting definitions, update settings use cases, and expose the warning setting in Settings.
4. Add a presentation confirmation service that can ask save/discard/cancel.
5. Add guarded navigation APIs/use cases so navigation is canceled before the stack changes.
6. Wire app menu/settings/about/store/home/recent/open-file navigation through guarded navigation.
7. Add main-window close protection using the same edit-session guard.
8. Rename edit export labels from `Save` to `Save as`.
9. Add unit tests for dirty-state, guarded navigation, settings, and view-model labels/commands.
10. Run build/tests.

## Acceptance Criteria

- Navigating from a dirty image/video edit page to Settings or About prompts before leaving.
- Closing the main window from a dirty image/video edit page prompts before closing.
- Choosing cancel preserves the current edit page and edit state.
- Choosing discard navigates/closes without exporting.
- Choosing save invokes the existing export flow before navigating/closing.
- Disabling warnings allows navigation/close without a prompt.
- Edit operations do not automatically overwrite the working file, original media, or captured output copy.
- All edit export controls read `Save as`.
- Automated tests cover the new application and presentation behavior.

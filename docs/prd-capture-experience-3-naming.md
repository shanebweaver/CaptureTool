# Capture experience — step 3: automatic capture names

Status: Approved direction; follows shared pane/content review.

## Outcome

New captures acquire useful names after existing local analysis produces a title. Users can override a name in the editor pane. The same chosen name identifies the capture in the app and is suggested when saving/exporting.

## Product contract

- One setting: Automatically name new captures. Explain that it uses local AI analysis. Enabling requires scanning and the existing shared local-AI consent; do not introduce another consent checkbox. When scanning is off, the setting cannot activate naming.
- Capture/save operations never wait for title generation. The existing filename remains the fallback while analysis is pending or yields no suitable title.
- This setting applies to newly captured assets while it is enabled. Turning it on does not rename the existing library. Reanalysis does not continually rename an established capture.
- An explicit user name always wins, including when entered while analysis is completing. Turning the setting off stops pending automatic application as well as future enrollment.
- A generated name is applied at most once from verified, current-source synopsis metadata. Do not generate another model response just for naming.
- Accepted names belong to the capture identity and persist independently of deletable analysis metadata. Track automatic versus user ownership. Persist names securely because they can contain captured content.
- Name editing is available without AI. Names are bounded single-line text; reject empty/manual invalid input with clear feedback.
- Use the chosen name consistently in editor/pane and recent-capture presentation, with the actual filesystem path still available in Details.
- Save/Export prepopulates a filename derived from the chosen name, preserving the target extension and applying Windows-safe filename normalization. The save picker remains authoritative for destination/collision confirmation.
- Existing files on disk retain their names. Do not silently rename imported files, earlier auto-saved files, or user-named exports. Technical retained-source filenames remain implementation details.
- Disabling naming or deleting analysis does not undo established names. Explicitly describe that distinction in settings copy.

## Architecture

Implement naming as an application feature consuming existing committed synopsis results, not a model-specific callback or a new analysis capability. Tie eligibility and chosen names to stable CaptureId and persist them atomically. A user edit must win even if background title application races it. No UI lifetime is required for automatic naming.

Ensure source-revision, analysis-deletion generation and current policy prevent a stale completion from applying a title after opt-out. Restarts recover eligible pending work without renaming unrelated historical captures. Name updates notify consumers without rescanning media.

Extend the existing file-picker abstraction with an optional suggested name rather than duplicating save dialogs. Keep file identity, source revisions and current saved-copy behavior unchanged.

## Acceptance and review gate

- New eligible captures gain one title; existing/ineligible captures and failed/empty analysis retain their fallback.
- User edit versus analysis completion, disable/re-enable, scanning off, deletion, source changes and restart are covered by meaningful tests.
- Clearing metadata retains chosen names; names cannot leak through unprotected metadata caches or logs.
- App display names and suggested Save/Export filenames agree, including Unicode, punctuation, reserved Windows names, maximum lengths and absent titles.
- Existing save/overwrite, trim/export, scratch cleanup and capture-identity tests continue to pass.
- Review the whole experience, run final managed/native checks and visual verification, update the review guide with honest limitations, and stop for the user's review. Do not merge or publish automatically.

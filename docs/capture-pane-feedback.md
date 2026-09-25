# Capture pane feedback

Replace the display-name workflow with suggestions that the user explicitly accepts
to rename the existing file. Suggestions must never change the visible file name
or a file on disk before acceptance. A successful rename updates the capture's
identity paths, recent history, and the open editor, preserving the extension and
refusing to overwrite another file. Failed or interrupted publication must remain
recoverable.

Remove the pane's refresh/retry action and its command plumbing; keep automatic
refresh as analysis changes. Remove visible checkbox text from standalone Text
Extraction settings, retaining its accessible name.

Reuse the existing image text-selection renderer for the pane Text tab, including
the dimmed image, text selection, and QR actions. The pane uses saved metadata and
does not run standalone OCR or change its consent. Clear the metadata overlay on
tab change, pane close, source edits, or stale/deleted results. Text rows should
prioritize their content and compact actions rather than repeating a source label.

Show per-capture loading status for the AI summary and name suggestion. Clear it
when the relevant step finishes, fails, is cancelled, or analysis is disabled.

Verification must cover physical rename and collision/failure recovery, editor and
recent-history paths, pending suggestions remaining unapplied, independent OCR,
overlay lifecycle, compact rows, and loading states. Run desktop UI checks in
English only.

Implemented and reviewed on 2026-09-25:

| Feedback | Implementation and verification |
| --- | --- |
| Remove retry | Removed the pane button, command, notifications, and localized strings. Analysis events still refresh the pane automatically. |
| Remove visible Allow | Removed the checkbox content resource while retaining its accessible name. The English settings check confirms the checkbox has no visible text child. |
| Share the image overlay | Saved metadata projects into the existing text/QR renderer without calling standalone OCR. Projection, source-validity guards, tab/pane lifecycle, and independent standalone OCR checks pass. |
| Dense text rows | Text is the primary content, with compact locate/copy actions and timestamps where relevant. The English UI check and screenshot review confirm the repeated source heading is gone. |
| Show generation status | Per-capture name and summary indicators follow active work and clear on completion, failure, cancellation, or policy revocation. Unit checks cover failed suggestion publication; the native UI shows both loading states. |
| Accept a suggested filename | Suggestions leave filenames and recent-history titles untouched until accepted. Acceptance and manual rename move the actual file, preserve its extension, update catalog/history/editor paths, and refuse collisions. An encrypted intent recovers interrupted metadata/history publication without repeating the file move. |

Final unit runs passed: 348 infrastructure, 424 application, and 291 presentation
tests (1,063 total). Rename tests include image/audio/video, source versus saved-copy
paths, collisions, failures before and after the move, restart recovery, and
case-only changes. The x64 native publish succeeded. All six resource catalogs
have matching sets of 680 nonempty keys with no duplicates; `git diff --check`
passed.

Focused desktop checks ran in English only using isolated fixtures. They cover
settings consent, onboarding, pane lifecycle, standalone text extraction, pending
suggestions, acceptance and manual rename, restart persistence, and physical rename
while audio/video editors are open. Screenshots of the text overlay, both loading
indicators, and the suggestion action were visually reviewed. Unit/build output is
recorded in `artifacts/feedback-*-final.log`; earlier native UI coverage is in
`artifacts/feedback-ui.log` and `artifacts/feedback-ui-recheck.log`.
The final rebuilt app also passed all three targeted rename checks (image naming,
open audio, and open video) in `artifacts/feedback-ui-final.log`.

Follow-up: removed the pane's inline copy-status row and state. Copy success and
failure, folder failures, and other pane action errors now use the existing app
snackbar. The manual rename form is one row: filename input, confirm icon, and
cancel icon. Removed the explanatory hint while keeping validation errors;
both icons have localized tooltips and accessible names.
Follow-up verification passed: 291 presentation tests, native x64 publish, and one
English desktop scenario exercising filename acceptance, the compact manual rename
form, and the copy snackbar. Both updated screenshots were visually reviewed.
All six resource catalogs now contain the same 681 nonempty keys. Logs are in
`artifacts/pane-controls-{tests,publish,ui}.log`.

# Capture experience implementation and review

Working branch: `codex/capture-details`. The three PRDs are [pane](prd-capture-experience-1-pane.md), [content](prd-capture-experience-2-content.md), and [naming](prd-capture-experience-3-naming.md).

## Step 1 review — complete

- The local Windows property reader is shared by the analysis adapter and editor. It does not invoke models or require consent.
- Basic properties and saved analysis load independently. Metadata deletion clears optional content without removing basic properties; disposal rejects late responses from both reads.
- All editors host the same adaptive pane. Opening it is explicit; its open preference lasts across editor navigation during the app session. The Home Details dialog is removed.
- Source properties are distinguished from pending edits. Clipboard and folder errors remain inline.
- Managed checks: 426 application tests and 280 presentation tests passed, including independent loading, deletion and late local-read cases.
- Native x64 compilation passed. The first desktop run exposed a missing automation node on the pane's layout root. Visual inspection confirmed that the content rendered; an explicit pane automation peer was added. Review also removed blank status spacing and restored toolbar focus on keyboard/close-button dismissal. The isolated English native desktop flow passed after these fixes; wide light and narrow dark screenshots were inspected. The final suite passed in all six locales.
- Reopening from Home exposed a working-copy identity issue. Audio/video now retain the original file path like images, and all editors expose a stable details source independently of their working/rendered files. Existing open-file tests now assert that original-path contract.

## Step 2 review — complete

- The pane now has Details and Text/Transcript tabs. Search and source filters define the complete copy scope; visual rows load in batches of 100. Previous/next stops at the boundaries and loads only the next batch when necessary.
- Consecutive identical video OCR frames and duplicate QR values retain their original occurrences. QR actions open only explicit HTTP(S) targets; summaries remain separate from literal source text.
- Working-copy verification gates source locations. Image geometry changes disable region jumps; recording timestamps respect duration/trim boundaries and use the existing playback owner. Source text remains readable when only the editor's working copy has changed.
- Refresh preserves unchanged passage objects and selection. Disposal and metadata deletion reject late reads. A media-ready transition retries early file-property reads.
- Managed checks: 427 application tests and 288 presentation tests passed. Native review caught missing accessible names after location labels gained a source/time layout; explicit names were added. The UI harness also now resolves pane strings and keeps DPI scope on the test thread.
- The three isolated English native image/audio/video flows passed. Screenshots confirm dense source rows, search highlights, the image outline and paused recording playback at 3 seconds. Visual review caught a recycled occurrence selection crossing row boundaries; a model guard and native filtering regression now cover it. Changing the selected passage also clears the previous image outline. The final suite passed in all six locales.

## Step 3 review — complete

- Names and automatic enrollment are durable capture properties in catalog schema v4, protected by the existing user-data protector. Older catalog identities, provenance and registration order are preserved.
- Naming uses committed synopsis notifications rather than UI progress. It applies a title once, recovers pending eligible work at startup, and does not schedule models. The enrollment epoch is sampled at capture intake; enabling later cannot enroll a queued historical capture.
- Disabling naming, scanning revocation and deletion fence pending application. Current source bytes, catalog source location, authorization and deletion watermarks are checked. User edits win atomically and chosen names survive metadata deletion.
- One consistent settings toggle, pane name editing, recent-file display names and optional shared-picker filename suggestions are connected. Physical files retain their paths. Names use bounded single-line input and Windows-safe Unicode filename suggestions.
- Review replaced progress-driven naming with a committed-result event, added a catalog source-location check, and verified that background title changes preserve an active name draft. Status refresh is serialized with naming commands so an older read cannot overwrite a newer setting; shutdown drains background naming and active commands. Name validation rejects malformed Unicode.
- Final managed checks: 430 application, 325 infrastructure, 290 presentation and 10 Windows file-details tests passed (1,055 total).
- All 16 native desktop flows passed: the pane and settings in six locales each, audio/video navigation, existing ad-hoc OCR, and the naming lifecycle. Naming coverage includes actual intake, automatic title, user override, recent label, unchanged path, metadata deletion and restart. Wide/light, narrow/dark, localized content and recording navigation screenshots were inspected.
- The final x64 native AOT publish and targeted naming check passed after the last status/shutdown and Unicode hardening. The publish had only the four already documented Betalgo vendor trim/AOT warnings.
- Final screenshot review caught a test-lifetime gap: the naming test could terminate the app as soon as Delete was disabled, before cleanup finished. The naming and pane tests now wait for completed deletion; the naming restart also verifies empty text and absence of the deleted summary. All seven strengthened native flows passed on the final build, including the pane in six locales and naming across restart.

## Verification limitations

Desktop UI fixtures provide deterministic layout/lifecycle coverage, not model-quality measurements. Physical ARM64, offline isolation, and spoken Narrator verification remain the previously documented environment limitations. No new model or provider is introduced by this work.

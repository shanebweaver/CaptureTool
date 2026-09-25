# PRD: Capture Analysis — Slice 4, Release Verification

Status: Planned. Depends on reviewed [slice 3](prd-capture-analysis-3-integration.md).

## Purpose

Prove the complete feature's reliability and deployment compatibility. Earlier
slices include their own meaningful tests; this slice verifies their integration
and closes actual defects instead of creating a separate test framework.

## Release gates

1. Run the complete managed test suite and the repository's required build/checks.
   Build packaged x64 and ARM64 release configurations with trimming/Native AOT.
   Inspect shipped dependencies for stable supported provider/runtime versions.
   Add a CI publish check for the app and existing provider harness that allows only
   the reviewed Foundry/Betalgo error-converter warnings (IL2026 and IL3050 in Read
   and Write). Match diagnostic and originating method, fail on unexpected warnings
   in those categories or publish errors, and retain logs. Test the check with known
   and unexpected diagnostics. Revisit the exception on dependency upgrades; remove
   it when no longer needed. This compile check requires no model downloads/device.
2. Exercise a capable Windows device and a limited-capability device. Validate
   preferred-model selection, compatible fallbacks, missing models, offline
   preparation, and unsupported OS/hardware without app startup failures.
   Close the remaining ARM64 runtime and available Windows AI OCR/description gaps;
   cross-publishing and unavailable-model results do not prove those inference paths.
3. Test representative images, silent and multilingual audio, and video with and
   without audio. Check metadata identity, normalized coordinates, timestamps,
   actual model provenance, bounded resource usage, and source revision accuracy.
   Include local file details (file size, capture versus filesystem dates, rotated
   image dimensions/aspect ratio, audio/video duration and format), and QR values,
   bounds, repeated occurrences, inverted codes, and successful empty scans.
   Unknown file properties must remain unknown rather than invented defaults.
   Verify historical recents import and v1/v2 catalog migration do not mistake
   recent activity for capture time; legacy unverified metadata dates read as unknown.
4. Interrupt the process during queue admission, preparation, execution, metadata
   publication, and deletion. Restart and verify recovery, idempotency, no stale
   publication, and no indefinite progress indicator.
   Clear temporary files during audio transcription: active leased chunks survive,
   subsequent chunks still decode, and cancellation/completion removes the lease.
   Check interrupted scratch recovery and refusal to accumulate locked leftovers.
5. Disable scanning, revoke consent, clear metadata, and request reanalysis during
   active work. Confirm durable policy, deterministic conflicts, and late-write
   rejection even if a provider ignores cancellation.
   Verify the single consent covers background scanning and interactive local AI
   tools, including OCR, descriptions, and editing. Disabling scanning preserves
   consent for interactive tools; revoking consent cancels both kinds of work.
   Exercise v1 consent migration, declined dialogs, revoke/regrant, and protected
   policy write failures. Every confirmed Delete clears all current metadata;
   its button is disabled while deletion runs and recovers after a reported failure.
6. Inspect application storage and diagnostics: derived content is protected at
   rest, no recognized-content logs, and source files untouched. Decoded audio may
   exist only as bounded working scratch; verify removal on normal exit and recovery
   after interruption, with incomplete cleanup reported/retried. Test corrupt/unknown
   schemas and protection failures without silent data reset.
7. Verify minimal settings and passive snackbar on light/dark themes, keyboard
   navigation, accessibility announcements, and supported locales. Completed scans
   with exhausted model failures produce a deduplicated notice; ordinary unsupported
   skips and successful fallbacks remain quiet, and loading always disappears.
   Check failure delivery when the next capture starts before the UI updates.
   After a successful settings retry, a later independent failure must be reported
   again even with scanning off; unchanged failures must not flood notifications.
8. Verify the existing image OCR feature with fresh, stale, edited, missing, and
   unreadable metadata. Saved OCR/QR must retain word/QR actions and include decoded
   QR values exactly once in Copy all text. Empty OCR and empty QR scans remain valid
   cached successes; older OCR-only metadata still scans for QR codes. Validate
   working-copy aliases and coordinate mapping on rotated and large images.
   Include QR-only metadata when OCR is unsupported, denied, or fails; keep saved
   results usable and missing OCR retryable. Compare saved/fresh OCR exclusion near
   QR bounds and ensure unpositioned text is copyable without an origin hitbox.
   No new analysis panels in editors or search results UX on Home are introduced.
9. Measure startup/queue discovery and settings responsiveness with a large capture
   library, including cancellation and delete during discovery. The current store
   enumerates protected per-capture documents under its gate; record representative
   latency and memory before deciding whether a small work index is necessary.

## Evidence and handoff

Decision agreed on 2026-09-24: the reviewed Native AOT vendor warning exception does
not block slice 3. Implement the automated warning guard in this release-verification
slice before shipping. Packaged x64 Native AOT provider success and one native error
path have been exercised; this does not establish compatibility for every SDK error
format. See the [evidence and rationale](capture-analysis-execution-review.md#native-aot-compatibility-exception).

Record exact commands, configurations, device capability, pass/fail results, and
any remaining limitation in a short verification report. Automated tests prove
behavior only within their exercised scope; do not treat fakes as real-model or
packaged-runtime evidence. Resolve release-blocking failures before shipping.

## Non-goals

No new analysis features, benchmark platform, model lab, search/index features,
or speculative refactoring. Expand checks when failures expose a concrete gap.

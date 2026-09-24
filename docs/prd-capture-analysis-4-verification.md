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
2. Exercise a capable Windows device and a limited-capability device. Validate
   preferred-model selection, compatible fallbacks, missing models, offline
   preparation, and unsupported OS/hardware without app startup failures.
3. Test representative images, silent and multilingual audio, and video with and
   without audio. Check metadata identity, normalized coordinates, timestamps,
   actual model provenance, bounded resource usage, and source revision accuracy.
4. Interrupt the process during queue admission, preparation, execution, metadata
   publication, and deletion. Restart and verify recovery, idempotency, no stale
   publication, and no indefinite progress indicator.
5. Disable scanning, revoke consent, clear metadata, and request reanalysis during
   active work. Confirm durable policy, deterministic conflicts, and late-write
   rejection even if a provider ignores cancellation.
6. Inspect application storage and diagnostics: derived content is protected at
   rest, no plaintext temporary output or recognized-content logs, source files
   untouched, and incomplete cleanup reported/retried. Test corrupt/unknown schemas
   and protection failures without silent data reset.
7. Verify minimal settings and passive snackbar on light/dark themes, keyboard
   navigation, accessibility announcements, and supported locales. Confirm the
   feature does not add analysis UX to editors or search UX to Home.

## Evidence and handoff

Record exact commands, configurations, device capability, pass/fail results, and
any remaining limitation in a short verification report. Automated tests prove
behavior only within their exercised scope; do not treat fakes as real-model or
packaged-runtime evidence. Resolve release-blocking failures before shipping.

## Non-goals

No new analysis features, benchmark platform, model lab, search/index features,
or speculative refactoring. Expand checks when failures expose a concrete gap.

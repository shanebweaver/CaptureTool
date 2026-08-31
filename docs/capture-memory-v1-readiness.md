# Capture Memory v1 architecture review

Date: 2026-08-31

## Decision

Keep the metadata-first architecture. Stable capture identity, a durable consent/enrollment
ledger, per-capability durable jobs, canonical metadata, and a disposable search projection
are appropriate for v1. The recent failures concern lifecycle coordination and presentation
freshness, not a need for another metadata database or a different AI provider.

The application services remain authoritative. Page state, button enablement, model preparation,
and activity animation must never grant consent or determine whether a job may commit.

## Findings and implemented hardening

| Finding | Change | Safety boundary |
| --- | --- | --- |
| Settings counted eligible enrollments only on load or after its own actions. Background enrollment could leave Reanalyze disabled indefinitely. Home refreshed policy only during backfill. | Both visible pages use the same cancellation-aware, one-second state refresh loop. Reads are sequential within the loop, recover from transient failures, and stop on navigation. Stale reads cannot overwrite a newer operation. | Refresh reads policy only; it does not enroll captures or run models. |
| UI and maintenance independently interpreted enrollment states. | Enrollment owns `IsMemoryCleared` and `CanReanalyze`; policy snapshots expose the derived counts. Maintenance, admission, and Settings consume those rules. | Explicit exclusions and forgotten/private captures remain excluded. |
| Disabled Reanalyze had no explanation. | Settings distinguishes unavailable status, an action in progress, consent off, no enrolled captures, and excluded captures. New copy is localized in all six shipped resource sets. | A disabled button is not bypassed just to make it clickable. |
| Reanalysis preparation was all-or-nothing across the library. A missing required speech model could prevent image OCR too. | Ready capabilities establish an authorized processing boundary per media kind; supported captures go through the normal per-capability scheduler even when other preparation fails. No-ready-capability media are not falsely reported as queued. Partial work has an explicit status. | Policy/source verification and commit fences still run in the normal pipeline; no new provider or remote fallback. |
| Settings progress delegates could mutate bindings from worker threads or arrive after completion/cancellation. | Progress captures the UI synchronization context and is scoped to the active operation. Late/cancelled callbacks are ignored. Unexpected failures unlock controls for retry. | UI progress does not control durable work completion. |
| The policy command wrapper discarded cleanup failure and could announce successful erasure. | Revocation remains committed, but incomplete/throwing cleanup returns `ReconciliationRequired`. | Data cannot be reauthorized merely because cleanup failed. |

Previously implemented: global erase uses recoverable MemoryCleared tombstones; explicit
backfill starts fresh enrollment generations after cleanup. Workers refresh the search projection
before job completion, and open search queries refresh after projection changes.

## What Reanalyze means

Reanalyze is an explicit retry/refresh of enrolled captures and recoverable cleared enrollments.
It is not an import-all button and must not silently undo per-capture privacy decisions.
Eligibility depends on durable enrollment, not whether an optional model happens to be available.
Model/source failures are operation outcomes, not reasons for a button to remain permanently disabled.

Preparation and scheduling completion mean work was queued, not that recognition has finished.
The main-window activity indicator reflects durable job processing. Successful jobs publish search
before completion; useful partial metadata remains searchable when another capability fails.

## Remaining release gates

1. **Legacy exclusion recovery decision:** older development builds wrote UserExcluded for both
   global erase and deliberate exclusion. Their provenance cannot be recovered from the ledger.
   Do not silently migrate those rows. Decide whether to ship a separately confirmed recovery
   action or leave those development-only records excluded. Private and forgotten records must
   never be included. This review does not change existing user data.
2. **Device-backed acceptance:** the tests below exercise contracts and simulated provider output;
   they do not prove actual recognition on every supported device. Verify image, audio, and video
   on a fully supported machine and one with unavailable capabilities, plus a non-English speech
   sample. Run the checklist using a packaged build.
3. **Upgrade/restart/cleanup soak:** verify interruptions during preparation, queueing, recognition,
   index publication, and cleanup. Exercise missing/locked originals and interrupted cleanup with
   explicit recovery status. Repeat across an app restart and an upgrade that retains user data.
4. **Store release build checks:** complete the repository's Release/AOT and architecture matrix;
   a Debug x64 build is not a substitute. No model SDK versions or provider release gates were
   changed by this hardening.

## Manual acceptance checklist

- Keep Settings open with Memory enabled and no enrolled captures. Take a new capture. Reanalyze
  must become available after background enrollment without changing pages (normally within one second).
- Reanalyze an image/audio/video library when speech is unsupported. Supported work must run;
  the status must distinguish partial queueing from full success and from recognition completion.
- Cancel preparation, navigate away/back, then retry. Controls must unlock; delayed progress must
  not restore a stale busy/preparing state.
- Turn off and erase, then enable with existing captures selected, three times. Search for known
  text without retyping while analysis finishes. Repeat from both entry points and after restart.
- Turn off while an old status read is in flight. Neither page may restore stale authorized state.
- Clear metadata without revoking consent. Reanalyze must remain available for cleared enrollments.
- Remove a capture from Memory. Neither reanalysis nor existing-capture backfill may restore it.
- Test legacy excluded data separately: expect the explanation, not automatic recovery.

## Follow-on architecture work

A single application-level enable/preparation/backfill workflow could reduce remaining duplication
between Home and Settings. A durable operation ID spanning preparation, queueing, analysis, and
projection would improve diagnostics and future cancellation UX. These are bounded follow-on
improvements, not reasons to replace canonical metadata or add a new queue before v1.

The current visible-page refresh loop intentionally reconciles durable state rather than relying
exclusively on in-memory events: enrollment and consent can change outside a page, and recovery
must not depend on receiving a particular notification.

# PRD: Capture Analysis — Slice 3, Capture and Settings Integration

Status: Implemented for review. See [integration review notes](capture-analysis-integration-review.md).
Depends on reviewed [slice 2](prd-capture-analysis-2-execution.md).
The Native AOT warning guard is assigned to [slice 4](prd-capture-analysis-4-verification.md);
the reviewed vendor exception does not block this slice.

## Purpose

Connect post-capture scanning to a minimal consent/settings workflow and passive
progress snackbar. Keep application policy out of page view models.

## Capture lifecycle

Register finalized image/audio/video captures with stable identity and enqueue
only while scanning and generic Capture Memory consent are enabled. Registration
must be durable before queue admission; startup reconciliation recovers a registered
capture whose admission was interrupted. Analysis failure never reverses capture
success. Report failed registration without silently claiming that it can be recovered.
Recovery cannot promise to discover files created before any durable capture record.

Assign identity once at capture finalization. Use the retained original as the analysis
source; auto-save updates the preferred location on that same identity, including
when it completes after registration. Duplicate finalization callbacks and recent
history migration must not create another identity for the original/saved pair.

Existing-capture scans include catalogued app-created captures with available
sources. Migrate identifiable captured-origin recent entries once/idempotently;
skip opened-origin entries, missing files, and unknown external files. Recent
history is bounded and cannot recover entries already removed from that history.
Do not scan arbitrary filesystem locations or migrate prototype analysis data.
Imported historical entries remain eligible only for an explicit existing-capture
scan, even if their catalog registration happens after scanning was enabled.

## Admission and startup reconciliation

Extend the protected capture catalog with a durable, monotonic registration sequence
and a current boundary query. Assign sequence and identity atomically; replay and
preferred-path updates keep the same sequence. Do not use timestamps or recent-list
position as ordering. Migrate known catalog versions explicitly while preserving
identities; unknown/corrupt versions still fail closed.

Capture completion also records whether automatic analysis was authorized and its
policy revision. Preserve that eligibility with registration; an off/unknown decision
cannot become eligible merely because a delayed callback runs after enabling scanning.
This is intake eligibility, not a second copy of queue progress or analysis results.

- On each transition from scanning off to on, persist the current catalog boundary
  with the enabled policy before offering backfill. Declining or cancelling that
  offer leaves only future captures eligible; it must not trigger historical scans
  at the next startup. Checking consent alone grants no scanning eligibility.
- Serialize registration, policy transitions, and the snapshot/commit part of clear
  through the application service. Clear passes the catalog boundary to
  `ICaptureAnalysisWorker.ClearAsync(long)`, which persists it with the new metadata
  generation. Captures at or below that boundary cannot be automatically recreated.
  Future captures remain eligible. Do not use the no-boundary clear overload here.
- Reconcile only available, app-created sources eligible under the current policy
  and above both the enable and clear boundaries. Existing pending runs keep their
  stored identity and authorization revision. Existing terminal runs are not retried
  automatically; missing metadata alone is never permission to scan.
- Automatic intake and a backfill command retain the authorization revision and
  metadata generation under which they were accepted. Verify the expected revision
  inside the worker's admission lease, extending its request contract as needed.
  A stale callback or batch must not acquire new authorization merely because the
  user disabled and then re-enabled scanning. Store generation/run checks still apply.
- An explicit scan can include historical captures and creates a new run with an
  expected prior run ID. Superseding an active run also requests cancellation of its
  old attempt. Preserve valid results until replacements commit. Admit captures in
  bounded units so disable/revoke/clear can interrupt a large backfill; do not hold
  a command gate across the entire batch, a dialog, or model execution.

The existing per-capture analysis document remains the sole durable queue/run state.
After interruption, resume admitted requests; historical captures not yet admitted
require another explicit scan. Do not add a separate persistent batch journal.

## Settings behavior

Both scanning and consent default off. One application service owns persisted
transitions, scans, cleanup, cancellation, and progress. Leaving Settings merely
unsubscribes from observation; it never cancels work.

Persist scanning, generic consent, authorization revision, and the enable boundary
as one versioned policy record using atomic persistence. The revision survives
restart and rotates on disable/revocation; re-enabling cannot authorize an old run.
Supply the real `IAnalysisAuthorization` from this policy, replacing the default deny
implementation. Missing/unreadable policy grants no analysis. Never expose a partially
saved enabled/consented combination or report a failed save as a successful transition.

| Action | Behavior |
| --- | --- |
| Enable scanning | Obtain generic consent if necessary, enable future scans, then offer to scan existing captures. Declining backfill keeps future scans on; cancelling consent keeps scanning off. |
| Disable scanning | Stop queued/active work and prevent late publication. Offer deletion if metadata exists; keeping it leaves scanning off. |
| Run analysis on existing captures | Disabled when scanning is off. Require consent, then explicitly schedule a new run for eligible captures. Preserve still-valid successful results until replaced. |
| Delete metadata | Enabled only when data exists. Confirm, invalidate previous work, and delete analysis data without touching media or scanning preference. Future captures remain eligible; old captures return only through an explicit scan. |
| Capture Memory consent checkbox | Generic consent for all background scanning. Revocation disables scanning and offers deletion. Checking it alone does not start work. |

Consent copy explains on-device processing, persisted derived data, and model
downloads. Do not introduce separate model consents or reuse interactive editor
tool consent as authorization for background scanning. Combine prompts where
practical and persist successful transitions before showing success. Storage
failure leaves the UI truthful and retryable. Serialize conflicting commands;
disable/revoke/delete must be able to stop existing work promptly.
Recheck policy/generation after a dialog returns; an outdated dialog result cannot
undo a newer disable, revocation, or deletion. Metadata commands and declined
consent dialogs must not discard a queued disable/revocation policy save; only a
newer accepted grant can supersede that denial. Enabling reads consent after
earlier queued policy writes so a pending revocation cannot waive its dialog.
A disable/revoke request immediately
blocks admission/publication in memory and requests cancellation; persist the change
before reporting success. If persistence fails, keep analysis blocked for this session
and report that the preference could not be saved rather than silently resuming work.

Deletion remains available when analysis files exist but cannot be read, or cleanup
is pending. Do not require successful metadata deserialization to enable or execute
explicit deletion. A read/initialization failure alone never authorizes a reset.

## Application lifetime and storage recovery

One application-lifetime owner loads policy, initializes the store, and observes the
singleton worker's `RunAsync` task. Catch/report analysis startup failures without
blocking capture or app navigation. Settings and snackbar subscribe to snapshots;
they never create workers. Shutdown cancels and awaits the runner before disposing
services. No model preparation or source analysis occurs without both policy gates.

Expose a small read-only storage status/presence query through the infrastructure
port. Include queued analysis records, unreadable stored data, and pending cleanup
when deciding whether deletion is available; a healthy empty control file alone
does not count as metadata. Unknown/unavailable status is not empty. View models
must not inspect paths or deserialize records to enable the Delete action.

`StorageUnavailable` ends the current worker loop. After explicit deletion/recovery
or a successful user-initiated retry of a failed storage operation, initialize the
store and restart the same singleton worker if needed, after its previous task has
finished. Recheck policy and resume only still-valid requests. Do not create a second
worker or bypass its retained guard against an invocation that has not stopped.
There is no automatic reset or tight restart loop. A committed clear with incomplete
physical cleanup reports an error. Keep the Delete metadata label unchanged and
disable the button while deletion is running; re-enable it after failure if data
remains. Every confirmed Delete request deletes all metadata present at that time,
including metadata created after an earlier deletion failed. The button never
switches to an old-data-only cleanup action.
Automatic cleanup recovery only removes old generations, preserving newer analysis.
Preserve scanning preference and capture media.

## Progress UX

Use the prototype's snackbar styling and existing AI loading indicator. Show one
short localized text label for preparation or capture analysis. No click action,
flyout, model list, details pane, dismiss button, or completion card. Disappear
when the actual workload reaches a terminal state, including skipped/failed work.
Use the existing notification mechanism for actionable failures. Observe one
application-owned snapshot; do not add page polling loops or operation journals.

While scanning and consent are enabled, show loading only for `Preparing` and
`Analyzing`. Hide it immediately on disable/revocation, and for `Idle`,
`ProviderUnavailable`, and `StorageUnavailable`; an unavailable provider can retain
a pending backlog without an endless spinner. Show it again when processing resumes.
Marshal updates onto the UI dispatcher, attach to the latest snapshot on navigation,
and deduplicate failure notifications instead of notifying once per queued capture.

Use resource-backed text, keyboard-accessible settings/dialogs, stable automation
IDs, and polite, throttled accessibility announcements.

## Non-goals

No editor analysis panes, Home search results, index rebuild controls, capture
deletion UI, model selection/management UX, or background screen observation.

## Acceptance

- Image/audio/video finalization enqueues only with both gates granted.
- All settings transitions, prompt cancellation paths, and disabled states match
  the table, including metadata retained while scanning is off.
- Repeated enable/scan/clear cycles work after navigation and restart.
- Crash after registration/before admission recovers once; crash after admission
  resumes the same run. Terminal, cleared, and declined historical work stays stopped.
- Disable/re-enable and clear races reject stale intake, dialogs, and backfill
  continuations. Capture registration concurrent with clear has a deterministic
  before/after boundary; captures created while scanning was off require backfill.
- Policy-save failure never reports success or resumes analysis in that session.
- Delete remains available for unreadable data/pending cleanup. Successful explicit
  recovery restarts a stopped runner exactly once without resetting policy or media.
- Auto-save and idempotent recent-history migration preserve identity and do not
  accidentally make historical or opened-origin media eligible for automatic scans.
- A provider ignoring cancellation cannot restore cleared results.
- Snackbar remains passive and disappears after real completion/unavailability.
- Navigation does not duplicate workers/subscriptions; provider recovery resumes
  progress, and repeated unavailable snapshots do not flood notifications.
- Focused view-model/use-case tests cover state transitions; desktop checks cover
  actual controls, localization, accessibility, and navigation.

Stop for review before [slice 4](prd-capture-analysis-4-verification.md).

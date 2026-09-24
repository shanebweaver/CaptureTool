# PRD: Capture Analysis — Slice 3, Capture and Settings Integration

Status: Planned. Depends on reviewed [slice 2](prd-capture-analysis-2-execution.md).

## Purpose

Connect post-capture scanning to a minimal consent/settings workflow and passive
progress snackbar. Keep application policy out of page view models.

## Capture lifecycle

Register finalized image/audio/video captures with stable identity and enqueue
only while scanning and generic Capture Memory consent are enabled. Registration
and startup reconciliation must recover the gap between finalization and queueing.
Analysis failure never reverses capture success. Auto-save preserves identity.

Existing-capture scans include catalogued app-created captures with available
sources. Migrate identifiable captured-origin recent entries once/idempotently;
skip opened-origin entries, missing files, and unknown external files. Recent
history is bounded and cannot recover entries already removed from that history.
Do not scan arbitrary filesystem locations or migrate prototype analysis data.

## Settings behavior

Both scanning and consent default off. One application service owns persisted
transitions, scans, cleanup, cancellation, and progress. Leaving Settings merely
unsubscribes from observation; it never cancels work.

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

Deletion remains available when analysis files exist but cannot be read, or cleanup
is pending. Do not require successful metadata deserialization to enable or execute
explicit deletion. A read/initialization failure alone never authorizes a reset.

## Progress UX

Use the prototype's snackbar styling and existing AI loading indicator. Show one
short localized text label for preparation or capture analysis. No click action,
flyout, model list, details pane, dismiss button, or completion card. Disappear
when the actual workload reaches a terminal state, including skipped/failed work.
Use the existing notification mechanism for actionable failures. Observe one
application-owned snapshot; do not add page polling loops or operation journals.

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
- A provider ignoring cancellation cannot restore cleared results.
- Snackbar remains passive and disappears after real completion/unavailability.
- Focused view-model/use-case tests cover state transitions; desktop checks cover
  actual controls, localization, accessibility, and navigation.

Stop for review before [slice 4](prd-capture-analysis-4-verification.md).

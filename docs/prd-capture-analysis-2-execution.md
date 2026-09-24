# PRD: Capture Analysis — Slice 2, Ordered Execution

Status: Implemented for review. See [execution review notes](capture-analysis-execution-review.md).
Depends on reviewed [slice 1](prd-capture-analysis-1-foundation.md).

## Purpose

Execute configured analysis reliably with stable on-device providers, without
coupling successful capture creation to AI availability.

## Requirements

1. One application-owned worker processes captures in FIFO order and steps in
   configured order. One active model invocation at a time is the initial limit.
2. Persist queue intent and completed steps with stable request/run identity.
   Deduplicate pending work by capture and requested run. Resume after interruption
   at the unfinished step under the same run ID, following the persistence contract
   below; do not introduce distributed leases or intra-model checkpoint infrastructure.
3. For each step, try candidates in configuration order. Probe OS, hardware,
   readiness, media, and language compatibility. Preparation/downloads require
   authorized processing; they never occur during passive availability probes.
4. Prefer the configured best candidate when usable. Apply bounded preparation
   and execution attempts, then try a compatible local fallback on eligible
   provider failures. Do not bypass content-safety refusals through fallback.
5. Empty valid output is success. Cancellation and invalid/missing source abort
   the relevant work, not trigger fallback. Permanent unavailability terminates
   that step so it cannot keep progress active forever.
6. Persist successful steps independently. A failed later step must not discard
   earlier success. Reanalysis replaces only successful results for the same
   revision and records the latest attempt outcome separately.
7. Verify source content revision before analysis and before publication. Use
   immutable retained sources or a verified snapshot while models read. Never
   publish results as current for bytes that changed during processing.
8. Enforce store write tokens plus enabled/consent state before admission and
   publication. Cancellation must not be the only defense against late results.
9. Expose one progress snapshot for preparation, queued/active captures, and
   terminal outcomes. Work belongs to the application lifetime, not a page.
   Serialize attempt validation and snapshot updates; deliver notifications in order
   so late/concurrent provider callbacks cannot restore loading after completion.
10. Dispose provider resources and temporary decoded media deterministically.
    Bound video frame sampling, audio chunking, memory, and retained scratch.

## Durable execution contract

Extend the existing analysis persistence boundary in this slice. Slice 1's
`BeginRunAsync` creates a new ID on every call and is not a resume operation;
retained successful results alone do not prove completion in the current run.

- The application assigns a request/run ID once for each newly authorized scan.
  Persist that ID, capture identity, admission generation, plan version, and queue
  order before signaling the worker. Admission checks the expected generation and
  capture state atomically. Repeating accepted admission is idempotent; a stale
  admission must not supersede a newer request. A new explicit scan gets a new ID.
- Separate new admission from resuming persisted work. Resume uses the stored ID
  and checks that the request is still current; it never starts a replacement run
  or adopts the current generation on behalf of an old request. Bind the verified
  source revision before model execution. Changed source or incompatible plan
  versions terminate that run explicitly rather than reusing its completion markers;
  further processing requires a newly authorized request.
- Persist each capture's request/run state, step outcomes, and canonical results
  together in one protected, atomically replaced document within its generation.
  Discover pending work from these durable records; an in-memory queue is rebuildable.
  Do not introduce a second authoritative queue/completion file or checkpoint service.
- Commit a successful payload and that run's completed-step outcome in the same
  atomic publication. Store producing run identity with each successful result;
  retained successes keep their original identity and do not count as current-run
  completion. Valid empty output is a completed success. Terminal skipped/failed
  steps persist their outcome without replacing a previous valid payload.
- On restart, completed steps are skipped using the current run's persisted
  outcomes. An invocation interrupted before its commit may run again; a committed
  step does not rerun because the process died before updating an in-memory cursor.
  Promise idempotent publication, not exactly-once model invocation.
- Clear invalidates pending requests as well as active write tokens through the
  same durable generation change, including deletion recovery. Check generation
  and current run when admitting, resuming, and publishing. Work from an older
  generation stays invalid across restart even if physical cleanup failed; it must
  never obtain a fresh token automatically. Enabled/consent checks remain additional
  gates. Cancelled/terminal requests cannot resume or accept late step commits,
  including after scanning is re-enabled. Persist the boundary needed by capture
  reconciliation with the generation change: missing analysis documents after clear
  must not cause startup to recreate cleared historical requests.

## Initial integrations

Review and adapt stable Windows AI OCR, legacy Windows OCR fallback, Windows image
description, and Foundry Local speech providers from the prototype. Keep SDK
references inside provider infrastructure. Pin stable versions and verify their
actual x64/ARM64, minimum OS, packaging, and Native AOT compatibility rather than
assuming the prototype's dependency graph is still appropriate.

Image order: text, description. Audio: transcription. Video: sampled-frame text,
audio transcription, selected-frame descriptions. Central configuration owns
candidate ordering and budgets. Video extraction is a shared media service, not
duplicated in each vendor adapter. Unsupported capabilities may be skipped while
the rest succeeds; inference never falls back to a remote service.

## Non-goals

No settings or search/editor UX, model-selection UI, quality-ranking engine,
arbitrary plugin discovery, dependency DAG, or model evaluation product.

## Acceptance

- Fake adapters prove deterministic step and fallback order, successful empty
  output, compatible-language selection, cancellation, and bounded failures.
- Crash/restart and duplicate-intake tests prove resumable, idempotent scheduling.
- Exercise crashes after admission but before worker notification, before step
  publication, and after publication but before advancing the worker. Assert stable
  run identity, repeatable unfinished work, and no rerun of committed steps.
- Reanalysis with the same source and plan cannot mistake a retained old result for
  current-run completion; include valid empty success and terminal failure/skip.
- Replay of an old admission/resume cannot supersede newer work. Clear followed by
  restart (including failed cleanup and recovered control) cannot resurrect pending
  work or re-admit it into the new generation. Test stale admission racing clear.
- Source mutation, superseded runs, and clear races cannot publish stale output.
- One failing/unavailable model does not block unrelated steps or future captures.
- If an invocation ignores cancellation beyond its drain budget, fail only the
  attempted capture. Keep untouched requests pending, report provider unavailability,
  and resume in FIFO order when it exits or after process restart. Never overlap
  model invocations. Cancel, clear, revoke, and shutdown remain responsive while
  waiting; discarded work cannot resume when the old invocation eventually exits.
- Packaged smoke tests exercise real available models and unsupported hardware
  paths without requiring every developer/test machine to have those models.
- Report preparation completion separately from recognition completion.

Stop for review before [slice 3](prd-capture-analysis-3-integration.md).

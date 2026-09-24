# PRD: Capture Analysis — Slice 2, Ordered Execution

Status: Planned. Depends on reviewed [slice 1](prd-capture-analysis-1-foundation.md).

## Purpose

Execute configured analysis reliably with stable on-device providers, without
coupling successful capture creation to AI availability.

## Requirements

1. One application-owned worker processes captures in FIFO order and steps in
   configured order. One active model invocation at a time is the initial limit.
2. Persist queue intent and completed steps. Deduplicate pending work by capture
   and requested run. Resume after interruption at the unfinished step; do not
   introduce distributed leases or intra-model checkpoint infrastructure.
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
10. Dispose provider resources and temporary decoded media deterministically.
    Bound video frame sampling, audio chunking, memory, and retained scratch.

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
- Source mutation, superseded runs, and clear races cannot publish stale output.
- One failing/unavailable model does not block unrelated steps or future captures.
- Packaged smoke tests exercise real available models and unsupported hardware
  paths without requiring every developer/test machine to have those models.
- Report preparation completion separately from recognition completion.

Stop for review before [slice 3](prd-capture-analysis-3-integration.md).

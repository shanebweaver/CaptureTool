# PRD: Capture Analysis — Slice 1, Foundation

Status: Implemented on `codex/capture-analysis-foundation`, awaiting review.
Feature branch: `codex/capture-analysis-core`.
See the [implementation and verification notes](capture-analysis-foundation-review.md).

## Purpose

Establish a small, provider-neutral foundation for post-capture analysis. Capture
Memory is the first consumer of its metadata; search and editor UI are separate
features. This replaces selected concepts from `agent/capture-analysis-stable-identity`
without merging that branch.

The delivery sequence is [foundation](prd-capture-analysis-1-foundation.md),
[execution](prd-capture-analysis-2-execution.md),
[integration](prd-capture-analysis-3-integration.md), then
[release verification](prd-capture-analysis-4-verification.md).
Each slice is reviewed before the next begins.

## Scope and architecture

- `Domain`: shared `CaptureId` only.
- `Domain.Capture`: durable capture identity and source/preferred locations.
- `Domain.Analysis`: source revision, capabilities, normalized versioned metadata,
  provenance, and result replacement rules. No IO, serialization, DI, or SDKs.
- `Application.Abstractions`: analyzer, capture catalog, metadata reader/store,
  and current-user protection ports. Provider types never cross these ports.
- `Application/Analysis`: immutable ordered plans and their common configuration.
- `Infrastructure`: protected atomic persistence and source-generated JSON DTOs.
- `Infrastructure.Windows`: Windows current-user data protection.

Analysis and Capture domains reference the shared kernel, not one another.
The application layer translates between their media classifications. There is
one metadata persistence boundary; do not introduce separate control, operation,
checkpoint, or search frameworks.

## Requirements

1. Configure image, audio, and video plans in one code location. A plan has a
   version and ordered steps; each step has an output capability, ordered model
   candidates, and bounded execution/preparation limits. No implicit quality scores.
2. Adding a model for an existing capability requires an adapter and configuration
   entry, not edits to orchestration, storage, or presentation. New output kinds
   require explicit domain contracts and serialization mappings.
3. Analyzer contracts expose availability, preparation, execution, cancellation,
   progress, and typed outcomes. Keep valid empty output distinct from failure.
   Availability checks must not download models or read capture content.
4. A durable capture catalog persists explicitly assigned identity independently of recent
   history. Auto-save changes the preferred location without changing the retained
   source or identity. Registration is idempotent; conflicting identity/location
   changes are rejected. Catalog operations never delete media.
5. Metadata identifies the capture, verified content revision, plan version,
   capability/schema, actual provider/model/adapter, and creation time. Provide
   typed OCR regions, descriptions, and timestamped transcript segments.
6. Store metadata and catalog data under application data using current-user
   protection. Serialize into memory, protect, then atomically publish encrypted
   bytes. Never write plaintext sidecars, temporary JSON, or recognized-content logs.
7. A new run invalidates previous write tokens. Preserve successful metadata when
   refreshing the same source; a changed source invalidates old results. Readers
   can require a particular source revision. Concurrent replacement and deletion
   must be serialized at the persistence boundary.
8. Clear metadata atomically changes a durable generation before cleanup. Old
   tokens are rejected, old generations are unreadable, and interrupted cleanup
   is retried on initialization. Cleanup failure remains visible and retryable.
   Do not delete capture files or the capture catalog.
9. Corrupt, unsupported, missing-control-with-existing-data, and protection-failure
   cases fail closed. Do not silently reset storage or overwrite unknown schemas.
10. Keep source-generated serialization compatible with trimming/Native AOT.
    Services may be registered now but must not start analysis or alter capture/UI
    behavior. Capture finalization, content hashing, consent, scheduling, and
    cancellation of real model work are wired in slices 2–3.

## Deliberate limits

Use the app's existing single-process lifetime and singleton stores. Atomic file
replacement protects against process interruption; this is not a distributed
database or a guarantee against disk failure. Windows current-user protection is
an at-rest boundary, not isolation from other software running as that same user.
No migration of the unreleased prototype's metadata is required. Older application
captures are enrolled explicitly in slice 3; do not crawl arbitrary folders.

## Acceptance and meaningful tests

- Configuration preserves both orders and rejects ambiguous/invalid plans.
- Catalog identity survives reload and auto-save; conflicts cannot corrupt it.
- All payloads and provenance round-trip through protected persistence.
- Failed protection/publication leaves the last committed record intact.
- A crash before the first control publication recovers unpublished temporary
  files without treating missing control over existing committed data as a fresh store.
- Successful results survive a failed refresh of the same source; changed sources
  and superseded runs reject stale writes.
- Clear races cannot resurrect metadata. Restart completes interrupted cleanup;
  a delayed cleanup cannot erase a newer generation.
- Corrupt/unknown records remain intact and do not silently become empty data.
- Use real Windows protection for an integration round-trip/tamper check, alongside
  deterministic failure injection for storage tests.
- Run affected application, infrastructure, and Windows infrastructure tests and
  build the affected graph. Record exact results before marking implemented.

## Review handoff

Keep this PRD commit on the parent feature branch. Implement on
`codex/capture-analysis-foundation`, then stop for review before slice 2.

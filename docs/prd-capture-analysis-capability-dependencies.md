# PRD: Capture Analysis capability dependencies and result lineage

- Architecture decisions: `ARCH-CAP-23`, `ARCH-CAP-24`
- Severity: High
- Status: Implemented
- Affected areas: Analysis Domain, orchestration, durable jobs, metadata persistence, analyzer contracts

## Summary

Capture Analysis must support multi-stage metadata pipelines without coupling features to model vendors or allowing analyzers to coordinate through hidden storage. A media-specific recipe will declare an acyclic graph of normalized capabilities. Durable provider-neutral jobs will carry those dependencies, analyzers will receive current normalized inputs, and every derived canonical result will record the exact upstream result instances it consumed.

Changing or removing an upstream result must invalidate every transitive dependent while preserving unrelated metadata. The same invariants must hold across application restart, legacy metadata migration, provider replacement, retry, and a source or policy race during inference.

## Problem

Independent OCR, description, and speech capabilities are sufficient for initial search, but higher-value metadata such as tags, entities, summaries, embeddings, and scene understanding will consume existing results. Treating those stages as unrelated jobs creates several failure modes:

1. a derived analyzer can run before its inputs exist;
2. a result can silently combine stale OCR with a newer description;
3. replacing an upstream model can leave derived metadata searchable indefinitely;
4. provider-specific orchestration or storage lookups can leak infrastructure concerns into Domain and Application contracts;
5. process restart can lose dependency intent or resume jobs in an inconsistent order;
6. legacy results without durable identities can receive a different identity on every read, making exact lineage impossible.

## Goals

1. Model capability composition as a validated directed acyclic graph in `CaptureAnalysisRecipe`.
2. Keep dependencies provider-neutral and defined in terms of app-owned capability schemas.
3. Schedule and persist dependency-aware intents deterministically.
4. Pass only current normalized canonical inputs to analyzers.
5. Give every canonical result a durable identity and record exact input references on derived results.
6. Reject commits whose declared inputs are missing, incomplete, or no longer current.
7. Recursively invalidate dependent results and outcomes when an upstream result changes.
8. Upgrade legacy metadata to stable result identities without requiring reanalysis.
9. Preserve existing behavior for recipes whose capabilities have no dependencies.

## Non-goals

- Selecting or implementing the first tags, entities, summary, or embedding model.
- Allowing runtime plug-ins or provider-defined dependency graphs.
- Persisting raw model responses as analyzer inputs.
- Running multiple providers concurrently for the same canonical capability.
- Adding a general workflow engine or event bus.
- Allowing dependencies across captures or source revisions.
- Making required capabilities depend on optional capabilities.

## Domain requirements

### Recipe graph

1. A `RecipeCapability` may declare zero or more exact `CapabilityDefinition` dependencies.
2. Every dependency must appear in the same recipe with the same capability ID, schema version, and classification.
3. Empty, duplicate, self-referencing, and cyclic dependencies are rejected when the recipe is constructed.
4. A required capability cannot depend on an optional capability.
5. Dependency edges participate in recipe semantic comparison.
6. The recipe exposes a deterministic topological execution order, preserving declaration order among otherwise independent nodes.

### Result identity and lineage

1. Every new canonical result receives a non-empty `CapabilityResultId`.
2. A derived result contains one `CapabilityResultReference` for each declared direct dependency and no undeclared, duplicate, or self reference.
3. A reference records the exact result ID, capability definition, analyzer revision, and UTC generation time.
4. Result equivalence includes its ordered input references but does not treat a newly proposed result ID as a payload change.
5. Rebasing unchanged source bytes preserves result identity and lineage.
6. Legacy results without persisted IDs derive a deterministic, stable ID from immutable canonical result facts. Repeated reads of the same legacy envelope must return the same ID.

### Aggregate invariants

1. A dependent result or terminal outcome cannot commit until every declared dependency has a canonical result.
2. A dependent result commit must reference exactly the currently canonical direct inputs.
3. A missing, extra, or stale input reference causes the conditional commit to be rejected.
4. Replacing or invalidating an upstream canonical result removes every transitive dependent result and outcome.
5. Updating an unrelated capability leaves other canonical results intact.
6. Restoring persisted metadata validates dependency provenance before exposing the record.

## Application requirements

1. The scheduler creates provider-neutral jobs in recipe topological order and copies exact direct dependencies into each job key.
2. Durable job equality and hashing include the normalized dependency set.
3. A worker that leases a dependent intent with missing inputs moves it to `WaitingForCapability` without selecting or invoking an analyzer and without consuming an attempt.
4. When an upstream result becomes canonical, direct waiting consumers for the same capture become runnable; transitive consumers progress as each level completes.
5. The worker passes dependencies through `CaptureAnalysisRequest.Inputs` in deterministic capability-ID order.
6. After inference, the worker commits references derived from the same in-memory input instances it passed to the analyzer.
7. The aggregate revalidates those references during the conditional metadata commit so an upstream race cannot publish stale derived metadata.
8. Analyzer adapters do not query the metadata store for dependencies and do not receive provider DTOs from upstream analyzers.

## Persistence and compatibility requirements

1. Recipe dependencies and durable job dependencies are persisted in source-generated JSON contracts.
2. Canonical result IDs and direct input references are persisted in protected metadata envelopes.
3. Empty dependency/input collections may be omitted to preserve the existing independent-capability document shape.
4. Existing job documents without dependencies deserialize as independent jobs.
5. Existing metadata without result IDs remains readable and receives stable deterministic identities.
6. A subsequent metadata write persists all result identities explicitly.
7. Unknown opaque capability entries continue to round-trip losslessly.

## Reliability and privacy requirements

- Dependency waiting survives process restart and lease recovery.
- No dependency transition can turn a successful capture into a failed capture.
- Result identity contains no source path, user content, provider response, or secret.
- Logs and job records contain capability identifiers and bounded failure information only.
- A provider kill switch affects resolution but does not weaken dependency or lineage validation.
- Invalid or corrupt persisted provenance is quarantined through the existing metadata corruption path rather than partially loaded.

## Test plan

Add focused coverage for:

- missing, duplicate, self-referencing, required-to-optional, and cyclic recipe dependencies;
- deterministic topological order and semantic comparison of dependency edges;
- dependent commit with exact inputs, missing inputs, stale inputs, extra inputs, and transitive invalidation;
- scheduler propagation and durable job round trips;
- worker waiting without analyzer invocation, normalized input handoff, wake after upstream completion, and commit-race rejection;
- metadata round trips for result IDs and input references;
- repeated reads of legacy metadata producing the same deterministic result ID;
- a later metadata write persisting the migrated ID;
- unknown capability preservation and existing independent recipes.

Run the complete managed test suite under the repository coverage command, enforce the 90% line-coverage gate, build WinUI x64, and build the ARM64/x64 Store bundle to validate source generation, trimming, and Native AOT.

## Acceptance criteria

- [x] Recipes reject invalid dependency graphs and expose deterministic topological order.
- [x] Dependencies participate in recipe semantics and durable job identity.
- [x] Dependent work waits without invoking a model until every direct input is canonical.
- [x] Analyzers receive only normalized, deterministic, current direct inputs.
- [x] Every canonical result has a durable ID and every derived result records exact direct lineage.
- [x] Conditional commit rejects missing, extra, or stale input references.
- [x] Upstream replacement recursively invalidates all transitive dependents and preserves unrelated results.
- [x] Job and metadata persistence survive restart and remain backward compatible.
- [x] Repeated reads of legacy metadata return stable result IDs, and a later write persists them.
- [x] Full managed tests pass at or above 90% line coverage.
- [x] WinUI x64 and ARM64/x64 Native AOT Store builds succeed without warnings or errors attributable to this work.

## Verification

Verified on 2026-08-12:

- the repository CI-managed command passed all 1,279 tests across ten test projects;
- Cobertura line coverage was 90.17% (14,650/16,247) and branch coverage was 76.39% (4,975/6,513);
- WinUI x64 Debug built with zero warnings and zero errors;
- ARM64 and x64 Native AOT MSIX packages and the combined Store `.msixupload` bundle were created successfully;
- `git diff --check` reported no whitespace errors.

## Rollout

The dependency contract ships before any production recipe uses a derived capability. Existing recipes remain dependency-free and behave exactly as before. Legacy result IDs are derived locally and deterministically when absent, then written explicitly on the next normal metadata update. No content reanalysis, consent expansion, or remote processing is required by this architecture change.

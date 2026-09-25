# Metadata enrichment foundation review

Date: 2026-09-24. Branch: `codex/capture-metadata-enrichment-foundation`.

## Delivered

The four PRDs were committed on the parent feature branch in `c076cea7`. This branch implements slice 1 only.

- Successful results now have durable result identities. Legacy results receive an identity derived from their canonical serialized representation; reads do not rewrite files, and the next ordinary write persists it. Keep that legacy identity representation stable when extending persistence DTOs.
- Derived results declare consulted first-level capabilities and exact result identities, including absent optional inputs. They cannot depend on another derived result or a different capture's record.
- Evidence resolves to a text entry and UTF-16 span. Existing OCR/QR bounds and transcript/video times remain on the referenced source payload. Spans cannot split a surrogate pair or overflow the source text.
- Structured facts have a bounded typed payload, literal source values, and evidence. Only OCR, decoded QR values, and transcripts qualify as evidence for observed facts. Generated descriptions remain eligible inputs for future inferred insights, not observed facts.
- Replacing an input atomically removes outputs that consulted it, including when an absent input becomes available or a replacement is empty. Unrelated updates preserve valid outputs. Both direct writes and durable step commits reject stale derived snapshots under the existing publication gate.
- Source changes, new runs, deletion, interrupted cleanup, and protected atomic publication retain their existing fencing and recovery behavior. No parallel store, scheduler, provider dependency, or settings were added.

## Validation

- Application test suite: **359 passed**.
- Infrastructure test suite: **271 passed**.
- **29 new cases** cover the domain and protected persistence behavior, including malformed documents, evidence grounding, Unicode bounds, immutable snapshots, optional inputs, stale commits, retained refresh results, source changes, restart, clear/cleanup retry, and protection/publication failure.
- Both projects compiled with the repository's AOT compatibility analysis enabled. Serialization uses explicit source-generated DTO mappings. Native executable publishing and hardware/provider tests were not rerun for this metadata-only slice; no new provider was added.

## Review findings and decisions

- A result identity is separate from a run identity: several capabilities can succeed in one run, and previous successful results can survive a same-source refresh. A replacement must have a new identity even if its text matches. Reusing a currently committed identity for replacement is rejected.
- Missing optional metadata must be recorded as a dependency; otherwise insights generated before OCR became available could appear current indefinitely.
- The domain validates dependencies and evidence on load as well as write. Malformed or stale on-disk records fail closed rather than exposing unsupported facts or silently discarding data.
- The compatible upgrade direction is old records into the new reader. Older binaries may reject newly written fields under their strict unknown-member policy; downgrade readability is not promised.
- This slice defines storage bounds (256 facts, 16 distinct evidence spans per fact, 4,096 UTF-16 units per value). Oversized payloads are rejected. Slice 2 must define extraction limits and explicit partial-coverage semantics before enabling a processor; it must not silently truncate to satisfy these bounds.

## Next slice

Slice 2 adds the bounded read-only metadata processor input and deterministic fact extraction with a precision-focused fixture corpus. Extraction, output coverage/normalization, and processor configuration are still unimplemented. Slice 3 adds typed semantic payloads and evaluates stable local text providers. Slice 4 connects reviewed processors to the existing worker and UX.

No enrichment step is configured or runs automatically yet. This branch is ready for the slice 1 review before advancing to slice 2.

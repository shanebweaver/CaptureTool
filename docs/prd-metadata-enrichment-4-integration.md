# Metadata enrichment — slice 4: pipeline integration and verification

Status: planned; depends on reviewed processor contracts and at least the deterministic facts adapter.

## Goal

Run enrichment as the final ordered stage of the existing capture analysis workflow, with the same policy, recovery, deletion, and progress guarantees.

## Integration contract

- Reuse the existing durable queue, ordered plan, authorization lease, cancellation, timeouts, bounded retries, and protected result commit. Avoid a second background scheduler.
- Extend central configuration to describe metadata steps and their declared first-level inputs. Validate that input steps precede consumers, candidates agree on capability/input contract, and no derived-on-derived cycles are possible. Fail configuration errors at composition time.
- Order: existing first-level steps; structured facts; title/summary; classification. A failed optional first-level step does not prevent processing the usable metadata that remains. Skip a derived step when it has no usable inputs; never record it as successful empty extraction.
- Snapshot selected metadata immediately before processor invocation, then verify its identities when publishing. Preserve the store's capture/source/run/generation checks and authorization fence. Do not hold the publication gate while a processor or model runs.
- On same-source reanalysis, unchanged older successful inputs remain eligible under existing refresh semantics. Preserve their original provenance so consumers can distinguish retained data from current-run success. A successful replacement invalidates its dependents before they are regenerated.
- A crash resumes the durable run without redoing successful preceding steps. Plan/processor changes require an explicit new run; do not silently recompute the library at startup.
- Existing captures gain enrichment through the existing Run analysis action. A separate metadata-only rescan command is out of scope for this delivery.
- One local-AI consent covers all processing. Scanning off/revocation prevents queued or active work from publishing. Turning scanning on and deletion prompts retain current behavior.
- Delete always deletes all basic and derived metadata. Late processor completions cannot resurrect either. Retrying physical cleanup cannot remove newly created results.
- Reuse the passive snackbar with simple localized text and no click action. Keep it active while a configured enrichment step runs and dismiss it when the entire run finishes. No model-specific progress controls or additional settings.

## Verification and release evidence

- Integration tests: each media type; missing/empty inputs; failed first-level/model steps; fallback order; timeouts and cancellation; disable/consent revocation; delete during generation; stale snapshots; source changes; same-source reruns; restart after each durable boundary; storage/protection failure; large inputs/backlogs.
- Validate any newly configured provider in real local execution and offline-after-preparation conditions. Separate unsupported hardware, model download failure, and provider failure in internal outcomes without storing capture text in logs.
- Compile applicable x64/ARM64 native AOT targets and validate packaged deployment/resources. Existing narrowly scoped vendor AOT exceptions do not automatically authorize new ones.
- Record real-hardware/runtime coverage honestly. No additional ARM64/Copilot+ device is currently available; compilation and mocks do not substitute for that runtime gate. Previously documented capture-analysis verification limitations remain visible.
- Regression-check OCR metadata reuse, basic scanners, settings enable/consent/run/delete flows, localization/accessibility, and the passive progress lifecycle. Use a representative corpus to review output usefulness and factual grounding separately from correctness tests.

## Completion

Document enabled capabilities, exact providers/versions, supported environments, corpus findings, operational limits, and remaining release gates. Merge the reviewed subfeature branch into `codex/capture-analysis-core`; do not merge to the default branch as part of this scope.

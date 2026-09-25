# Metadata enrichment integration review and verification

Status: all four slices implemented and reviewed; available local verification passed. Parent: `codex/capture-analysis-core`. Implementation branch: `codex/capture-metadata-enrichment-complete`, including foundation `2ad57deb` and facts `5cb6024e`.

## Delivered architecture

The existing durable worker now runs media scanners followed by structured facts, synopsis, and classification. Default plan versions are `image-v5`, `audio-v3`, and `video-v5`. `CaptureAnalysisConfiguration` composes the ordered plans; `MetadataEnrichmentConfiguration` owns enrichment order, candidate preference, declared inputs, processor version and budgets. Adding a Foundry text candidate requires configuration only. Other providers implement `IMetadataProcessor` and register through composition.

Configuration rejects duplicate provider identities, mixed candidate contracts, metadata output impersonating a basic capability, and media steps placed after enrichment. Processors can consume only supported first-level text capabilities, so derived-on-derived cycles are excluded. Optional absent sources remain explicitly represented. Metadata processors receive bounded immutable entries, never media paths, stores, policy services, or UI.

The worker shares readiness/preparation, sequential fallback, deadlines, cancellation, authorization, source validation and progress between scanner types. It selects entries for each attempt from the committed record; store/run/generation and exact present/absent input identities are checked again at publication. No publication gate is held while a model runs. Empty/missing usable input skips preparation and execution, rather than recording an empty successful extraction. Invalid metadata is a failed step, not an invalid media file. Malformed results and evidence are rejected before publication and can fall back.

Protected source-generated serialization supports both semantic payloads and coverage, retaining the earlier legacy-identity representation. Replacing a basic result invalidates dependents atomically. Same-source refreshes may consume retained successful inputs with their original identities/provenance. Delete removes all basic and derived metadata; delayed output cannot recreate it. Restart resumes committed step boundaries. Changes to processor contracts, prompts, candidate order or budgets must also bump the affected explicit plan versions. A queued old plan then fails with `plan-changed` and requires a new run; startup does not automatically enrich completed historical captures.

The existing scanning toggle, shared consent, Run analysis, Delete metadata, OCR reuse and passive snackbar remain the only UX. No editing-page analysis surface, home-search UI, automatic rename, new setting, or additional consent was added. The UI test composition uses deterministic providers and never initializes a real model.

## Semantic qualification

See the [provider decision](metadata-enrichment-provider-decision.md) for exact model IDs, licensing links, runtime requirements and deliberate limitations. The initial semantic candidate is CPU Phi-4 Mini through the already installed stable Foundry Local SDK. Results are suggestions with source-entry evidence; they are not observed facts or executable instructions.

The synthetic corpus includes image invoices, audio conversation with an undecided plan, video errors, German, filler speech, hostile instructions, contradictory source entries and omitted oversized input. Managed execution produced 15 valid payloads and one rejected hostile synopsis. Benign summaries preserved the supplied amounts, dates and uncertainty; conflicting statuses were explicitly summarized as conflicting. German summaries may be English. Topic/category quality is coarser than literal extraction, and the measured broader labels are documented in the provider decision. Schema/evidence acceptance alone is not a quality guarantee. A larger real-capture review belongs before exposing new consumer UX.

The harness preserves provider status separately from fixture acceptance. An expected abstention or safe rejection for noise/hostile input is not reported as successful generation. Early candidate/protocol reports remain in `artifacts/enrichment`; those failures were not silently discarded. Production logging never contains source or generated text. The diagnostic probe uses a hard-coded synthetic invoice only.

## Verification evidence

Reports are local artifacts, not committed binaries or private data.

- Managed suite: **1,145 passed, zero failures**, across all seven managed test projects (`artifacts/enrichment/managed-tests-summary.json`). Includes all media kinds, missing/empty input, fallback, deadlines, same-source refresh, deletion/revocation/source changes during generation, storage protection failures, corrupt documents and evidence, and restart without rescanning committed sources.
- Actual process interruption: all 10 checkpoints passed with Windows DPAPI and the native x64 harness, including enrichment execution, interrupted encrypted publication, and committed derived output (`artifacts/enrichment/recovery-final/recovery-results.json`).
- Native AOT: app and harness published for x64 and ARM64. Only the existing four narrowly scoped Betalgo converter warnings per target were accepted; no new suppression/dependency. Logs/resources: `artifacts/enrichment/native-aot-final`, with final harness builds in `artifacts/enrichment/harness-final`. The final x64 harness passed both native invoice checks with explicit fixture acceptance (`final-harness-smoke.json`) and all ten recovery checkpoints.
- Native UI: all 7 OCR/settings tests passed against the published app, including all six supported languages, consent/navigation/run/delete, passive progress lifecycle and UI Automation events (`tests/CaptureTool.UiTests/TestResults/enrichment-ui.trx`).
- Real native model corpus: 15 validated payloads and one safely rejected hostile synopsis, matching managed execution (`phi4-native-corpus.json`, `phi4-managed-corpus.json`). The initial harness exit was 1 because it counted every provider rejection as a fixture failure; provider outcomes are preserved, and the final harness distinguishes expected rejection from failed fixture acceptance. Existing basic-provider regression: 23 successful checks and four hardware-unavailable Windows AI checks (`basic-provider-results.json`).
- Final MSIX: x64/ARM64 native executables have the correct PE architectures and no CLR directory. All six languages are embedded in both main packages. Final package build and localization inspection passed (`package-final.log`, `package-inspection.json`, and the bundle under `artifacts/enrichment/package`). The 11 diagnostic-guard self-tests passed. The installed app was not replaced.
- Scale: 100/1,000/10,000 protected records passed. At 10,000: discovery 7.675 s, settings wait 49.9 ms, cancellation 0.8 ms, deletion during discovery 925 ms, process peak working set about 80 MB. Discovery allocated about 1.84 GB cumulatively. Linear discovery remains an existing scaling limit. These are local observations under concurrent verification load, not performance guarantees (`artifacts/enrichment/scale/scale-results.json`).

## Remaining environment/release gates

Native ARM64/Copilot+ inference, actual offline network isolation (fresh and prepared caches), and spoken Narrator behavior remain unavailable or unverified here, as already documented in [capture-analysis verification](capture-analysis-verification.md). Cross-compilation, UI Automation and successful cached online execution do not replace those checks. No additional ARM64/Copilot+ device is available. Hosted CI/Store workflows have not run on this local branch.

These outstanding device/environment checks remain explicit release evidence gates.

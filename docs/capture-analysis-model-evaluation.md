# Capture Analysis model evaluation and release gates

## Decision

Capture Memory remains provider-neutral. Product code requests a versioned analysis capability through `ICaptureAnalyzerResolver`; it does not name Microsoft, a model, or an adapter. `CaptureAnalyzerResolutionPreference` is an application policy that can rank an alternate adapter without changing Capture Memory feature code. Provider and analyzer kill switches are evaluated before an availability probe, and the existing processing policy still rejects a remote boundary before provider code runs.

CaptureTool does not use Windows Search, an operating-system content index, or a vendor-owned product index. The only product search index is the disposable, app-owned `CaptureMemorySearchProjection`. Model evaluation is a separate offline tool and cannot write through `ICaptureAnalysisStore`, the mutation coordinator, or the product projection.

## DDD placement

| Concern | Owner | Rule |
| --- | --- | --- |
| Capability and analyzer contracts | Application Abstractions | Stable across providers; includes provider/model/adapter/runtime/config identity. |
| Provider choice | Application | Resolver applies internal preference, quality, boundary, workload, and deterministic identity ordering. |
| Authorization and kill switches | Infrastructure policy | Platform, provider, and analyzer flags all must allow execution. Unknown providers and analyzers fail closed. |
| Windows AI adapters | Windows Analysis Infrastructure | Translate Windows model results into the same capability payloads used by every adapter. |
| Canonical metadata commit | Existing mutation coordinator | Only a successful, still-authorized job can replace a canonical capability result. Evaluation never participates. |
| Product search | Application | App-owned, disposable projection over canonical protected envelopes. |
| Experimental comparison | `tools/CaptureTool.Analysis.Evaluation` | Reads a versioned corpus and provider-run contract, emits only expiring evaluation reports. |

This keeps the dependency direction toward contracts. A future provider supplies `ICaptureAnalyzer` implementations and optionally registers `CaptureAnalyzerPreferenceRule` values. It does not add a branch to Capture Memory.

## Provider controls

Capture Analysis is off by default through `CaptureAnalysis_Platform`. Debug composition enables that flag and `CaptureMemory_Search` locally for model development; Release retains the generated off defaults. The built-in provider then has its own `CaptureAnalysis_Provider_MicrosoftWindows` kill switch, and each packaged adapter has an independent analyzer switch. All built-in provider switches default on so enabling the hidden platform flag produces the current behavior; any provider or adapter can still be disabled independently. Adding an adapter requires all of the following:

1. A unique analyzer identity and versioned capability result contract.
2. An explicit provider flag and analyzer flag. Unknown identities are not authorized.
3. A local-only descriptor unless a separate remote-processing architecture and privacy review has been approved.
4. A packaged provider-manifest entry and x64/ARM64 Native AOT smoke evidence.
5. A versioned evaluation run that meets the release gates.

Changing flags or preference policy requires incrementing `ResolutionPolicyRevision`; queued work then re-resolves instead of committing under stale selection policy.

## Offline evaluation contract

`Corpus/v1/corpus.json` is synthetic. A separately approved fixture may be used only when `isSyntheticOrSeparatelyApproved` records that approval decision. Product captures, production metadata, paths, telemetry, prompts, labels, and training data must never be copied into this corpus by default.

Each provider adapter emits the same `ProviderEvaluationRun` JSON contract. The contract records:

- corpus, query-set, provider, model, adapter, and configuration versions;
- processing boundary, device class, and cold/warm run mode;
- OCR and description output per fixture;
- preparation and analysis latency, peak working set, CPU, optional GPU/NPU counters, output size, and bounded failures;
- ordered product-search results and latency for every query;
- protected projection storage and indexed item count; and
- packaged Native AOT smoke results for x64 and ARM64.

Provider-specific collection code stays outside the evaluator. That lets current and future adapters alternate through one input contract, while the evaluator and gates stay identical. A checked-in run is a reproducible baseline contract; promotion evidence should be captured on the named device class, reviewed, and versioned rather than edited by CI.

Run the evaluator from the repository root:

```powershell
dotnet run --project tools\CaptureTool.Analysis.Evaluation\CaptureTool.Analysis.Evaluation.csproj --configuration Release -- evaluate --corpus tools\CaptureTool.Analysis.Evaluation\Corpus\v1\corpus.json --run tools\CaptureTool.Analysis.Evaluation\Corpus\v1\microsoft-windows-baseline.json --output .tmp\capture-analysis-evaluation --retention-days 30
```

The output records SHA-256 hashes of both input documents so results can be reproduced exactly. A gate failure returns process exit code `2`; invalid or unauthorized input returns `1`.

## Initial gates

| Gate | Threshold |
| --- | ---: |
| Precision@1 overall | at least 0.80 |
| Recall@5, exact text | at least 0.95 |
| Recall@5, descriptive | at least 0.75 |
| nDCG@5 | at least 0.80 |
| No-match false-positive rate | at most 5% |
| Bounded provider failure rate | at most 5% |
| Warm p95 search, at least 1,000 items | below 150 ms |
| Protected storage, at least 1,000 images | below 50 MiB, excluding model packages |
| Packaged Native AOT smoke | x64 and ARM64 both pass |

OCR character accuracy, preparation cost, analysis p95, memory, CPU, GPU/NPU, and provider-output size are always reported even when they do not yet have a promotion threshold. New thresholds require a corpus/version change or an explicit gate-version change, never a silent reinterpretation of an old report.

## Experimental isolation and retention

An output directory becomes manageable only after the tool creates and verifies `.capturetool-evaluation-root`. A nonempty unmarked directory is rejected. Every immutable run lives one level below that root, its report declares `capturetool.analysis.evaluation/v1`, and its expiry is explicit. `prune` deletes only an immediate child whose report has the matching run id, namespace, and expired timestamp:

```powershell
dotnet run --project tools\CaptureTool.Analysis.Evaluation\CaptureTool.Analysis.Evaluation.csproj --configuration Release -- prune --output .tmp\capture-analysis-evaluation
```

These files are app/tool-created non-user content and may be deleted under the approved derived-content policy. Experimental results never replace canonical metadata automatically. Provider failure, gate failure, expiry, or pruning therefore cannot delete or corrupt a production result.

## Packaged Native AOT gate

`CaptureAnalysisProviders.json` is packaged with the WinUI app. CI builds the combined x64/ARM64 Store bundle and `verify-capture-analysis-provider-smoke.ps1` independently checks each architecture for:

- the Native AOT app executable and native symbol output;
- the on-device provider manifest;
- the evaluated provider id; and
- the complete built-in adapter set.

The script also requires the evaluation run to declare a passing smoke for both architectures. This is a package-structure/AOT compatibility gate; device-specific model preparation and quality measurements remain part of the versioned offline run.

## Remote providers

The v1 evaluator rejects any boundary other than `on-device`. A remote provider is out of scope until a separate architecture and privacy review defines explicit consent, network authorization, data minimization, retention, deletion, telemetry, prompt/label/training restrictions, and failure behavior. Local-only product policy always wins over preference and provider availability.

# PRD: #483 Multilingual on-device speech recognition

- Status: Implemented; Nemotron preferred with Whisper fallback
- Date: 2026-08-27
- Parent architecture: [Capture Analysis Platform and Capture Memory](architecture-capture-analysis-platform.md)

## Outcome

Improve searchable speech from audio and video captures without adding a remote boundary or a preview dependency. Release behavior prefers stable Foundry Local Nemotron multilingual ASR and retains stable Whisper Tiny as the deterministic on-device fallback.

## Release-safety decision

Microsoft documents live Nemotron transcription and the multilingual alias in stable Foundry Local 1.2.x releases and current Microsoft Learn guidance. Capture Tool consumes only `Microsoft.AI.Foundry.Local.WinML` 1.2.4. The Foundry Local CLI is separately labeled preview and is neither referenced nor packaged. If Microsoft later marks the SDK API or model preview/experimental, the Nemotron kill switch must be disabled and Whisper becomes the only eligible speech analyzer until a new release review.

## Product requirements

1. Audio and video media remain on device and produce the existing `speech-transcript/v1` payload.
2. Nemotron is enabled, has the higher resolver quality tier, and is the first analyzer considered when ready.
3. Whisper remains enabled and is the deterministic fallback.
4. Whisper receives an explicit language hint derived from Capture Tool's selected UI language through the reviewed allowlist `de`, `en`, `es`, `fr`, `ru`, and `zh`. Unknown/system languages use deterministic `en`; unconstrained language auto-detection is not used in the fallback path.
5. Supported PCM and IEEE-float WAV inputs are downmixed and resampled in bounded windows to 16 kHz, mono, 16-bit PCM before transcription.
6. Input is divided into windows of at most 15 seconds. Native timestamps are offset to the original capture timeline; full-text-only results receive the bounded window range.
7. Preferred Nemotron uses alias `nvidia-nemotron-3.5-asr-streaming-multilingual-0.6b` through the stable live-audio SDK with a bounded push queue.
8. Nemotron uses multilingual auto-detection. The UI-language hint remains a fallback default rather than a claim about the recording; a future independent speech-language selector may override it only through a reviewed BCP-47 allowlist and may not infer language from filenames, account data, or unrelated capture metadata.
9. Nemotron normalization failure, runtime failure, or an empty final transcript produces a terminal preferred-attempt outcome, allowing the provider-neutral worker to try Whisper immediately within the same local-only intent.
10. Per-alias provenance persists independently and records the resolved model id/version, device, execution provider, catalog fingerprint, adapter version, language policy, streaming mode, and normalization/fallback policy.
11. Memory pressure unloads all loaded speech models without deleting model caches or canonical metadata.
12. Logs remain content-free and never include audio, transcript text, source paths, filenames, language hypotheses, or raw provider responses.
13. Neither adapter may introduce a prerelease NuGet package, preview CLI payload, local REST service, or remote fallback.

## Rollout controls

| Analyzer | Feature flag | Release default | Resolver tier |
|---|---|---:|---:|
| Nemotron multilingual | `CaptureAnalysis_NemotronMultilingualSpeech` | On inside the consent-gated Capture Analysis feature | 50 |
| Whisper Tiny | `CaptureAnalysis_Analyzer_FoundryLocalSpeechTranscript` | On | 40 |

Nemotron's higher tier makes it the preferred ready analyzer. During background work, a ready Whisper model may be used while Nemotron still needs explicit preparation. Disabling the Nemotron flag causes new intents to resolve directly to Whisper. Changing either analyzer's language, normalization, streaming, or fallback policy advances its configuration fingerprint; changing preferred-analyzer eligibility advances `ResolutionPolicyRevision` so queued work re-resolves.

## Release-quality gates

The enabled Nemotron experience must retain versioned, synthetic or separately approved multilingual evidence for all of the following on named x64 and ARM64 device classes. A failing gate disables the Nemotron kill switch without removing Whisper search:

- no language cohort regresses Whisper word error rate by more than 2 percentage points;
- the target multilingual cohort improves word error rate by at least 15% relative;
- no-speech false positives remain at or below 5%;
- timestamp median absolute error is at or below 500 ms and p95 is at or below 1.5 seconds;
- bounded provider failure rate is at or below 5%, with verified immediate Whisper fallback;
- 60-minute fixtures complete with bounded working set and no unbounded queue growth;
- cold preparation, warm latency, real-time factor, peak working set, CPU/GPU/NPU use, model size, and power impact are reported;
- the exact catalog model license and redistribution/use terms are reviewed and disclosed before model download;
- x64 and ARM64 Store Native AOT package smoke passes with no prerelease package or CLI payload.

## Acceptance evidence

- Unit tests verify all six localized UI-language mappings, deterministic unknown-language fallback, Whisper's explicit hint, normalized input, source-relative chunk timestamps, and language result fallback.
- Unit tests verify Nemotron's exact alias, stable live-PCM mode, auto language policy, empty-result fallback, distinct identity, and higher preferred tier.
- Unit tests verify PCM and IEEE-float WAV normalization and independent per-alias provenance.
- Feature tests verify Nemotron is enabled by default, its kill switch fails closed, and the resolution-policy revision advances.
- Provider manifest and package smoke require both packaged analyzer declarations.

## Ongoing release evidence

- Add the approved multilingual/noise/accent corpus and checked-in evaluation runs.
- Add durable per-window checkpoint resume for multi-hour fixtures; current work is memory-bounded but restarts an interrupted analyzer attempt.
- Complete model license/disclosure review and real-device x64/ARM64 measurements.
- Run Store package AOT smoke after the evaluation evidence names this adapter.

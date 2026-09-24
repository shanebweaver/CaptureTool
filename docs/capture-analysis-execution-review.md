# Capture Analysis execution: review notes

Date: 2026-09-24. Scope: [slice 2](prd-capture-analysis-2-execution.md).

## Branches and scope

The reviewed foundation and deletion-recovery changes were merged locally into
`codex/capture-analysis-core` at `cfb371d4`. This slice is on
`codex/capture-analysis-execution`, branched from that commit. Review against core.
Nothing has been pushed or merged back into core for this slice.

The application now composes an ordered durable worker and stable local providers.
Registration performs no scans or model preparation. The worker is not started by
the app yet, and its default authorization implementation denies all work. Settings,
consent persistence, capture intake, and the passive snackbar belong to slice 3.
No editor analysis UI or Home search UI was added.

## Implementation map

| Concern | Location |
| --- | --- |
| Ordered plans, candidates, preparation/execution budgets | [CaptureAnalysisConfiguration](../src/CaptureTool.Application/Analysis/CaptureAnalysisConfiguration.cs) |
| Durable run identity and ordered completion | [AnalysisRun](../src/CaptureTool.Domain.Analysis/AnalysisRun.cs) |
| FIFO processing, fallback, authorization, progress, bounded attempts | [CaptureAnalysisWorker](../src/CaptureTool.Application/Analysis/CaptureAnalysisWorker.cs) |
| Atomic admission/result/completion, restart and clear fencing | [Execution store](../src/CaptureTool.Infrastructure/Analysis/Persistence/LocalCaptureAnalysisStore.Execution.cs) |
| Streaming content hash and retained-source read lock | [LocalAnalysisSource](../src/CaptureTool.Infrastructure/Analysis/Sources/LocalAnalysisSource.cs) |
| Windows OCR/description and Foundry speech adapters | [Windows providers](../src/CaptureTool.Infrastructure.Analysis.Windows/) |
| Shared bounded image/video/audio decoding | [WindowsAnalysisMedia](../src/CaptureTool.Infrastructure.Analysis.Windows/Media/WindowsAnalysisMedia.cs) |
| Reproducible synthetic real-provider checks | [Smoke harness](../tools/CaptureTool.Analysis.Smoke/README.md) |

## Execution and persistence

One application worker processes captures by persisted queue order and capabilities
in plan order. Its wake-up signal is disposable state; pending work is reconstructed
from the protected capture documents. Admission is idempotent for the same request,
and replacing a request compares both generation and expected prior run identity.
Updating the queue counter before admission can leave harmless gaps after a crash.

Run intent, source revision, completed steps, outcomes, and canonical results share
one encrypted atomic document. A successful result carries its producing run ID.
Retained same-source results keep their original provenance and never stand in for
current-run completion. Failure/skip preserves existing valid payloads; empty success
replaces them. Readers reject inconsistent success markers. Restart resumes the same
run at its unfinished step. A committed step is not repeated; an interrupted model
invocation may repeat. Legacy write APIs cannot bypass managed execution state.

SHA-256 is streamed before execution and rechecked before each publication. The source
lease holds a Windows read-only sharing lock while adapters consume that path, blocking
replacement/writes. Source changes, missing/invalid media, cancellation, and content
refusal do not trigger alternative-model attempts. Content refusal still permits the
next independent capability. Eligible provider failures, unsupported models, and
cooperative timeouts advance through configured candidates. All attempts terminate.

Clear publishes a new generation before cleanup and cancels active work afterward.
Old admissions, queued work, completed-step writes, and late results remain invalid,
including across restart or failed physical cleanup. Cancellation callbacks cannot
make a completed clear fail. Consent/enabled authorization is a separate gate before
admission, source binding, and publication. Short authorization leases serialize those
operations with policy changes; inference does not hold the lease.

Progress distinguishes preparation from analysis, counts queued captures and completed
steps, records terminal run status, and becomes idle only when the queue drains.
Late provider progress and throwing observers cannot revive or stop the worker.
Corrupt/protection/IO failures stop the loop with `StorageUnavailable`; the worker does
not reset data or repeatedly retry failed storage.

## Provider choices and bounds

- Windows AI OCR and image description use the existing stable Windows App SDK
  packages (2.2.0 / AI 2.2.3); legacy Windows OCR is the text fallback.
- Foundry Local WinML is pinned to 1.2.4. The current catalog resolves
  `nemotron-3.5-asr-streaming-0.6b` and `whisper-tiny`. Actual model IDs/versions are
  stored with results. Initial speech execution explicitly uses CPU variants; GPU/NPU
  tuning and extra execution-provider downloads are outside this slice.
- SDK initialization, catalog lookup and downloads happen only during authorized
  preparation. No remote inference endpoint or local web service is started.
- Shared decoding scales images/frames to at most 2048 pixels per dimension. Video
  text samples at most 64 frames; descriptions at most 8, distributed within the
  timeline. Container duration is not treated as a decodable last-frame timestamp.
- Sources are capped at 4 GiB; audio/video duration at two hours. Speech uses one
  15-second, mono 16-kHz PCM16 chunk at a time, at most 1 MiB per WAV. Streaming pushes
  have a bounded queue. OCR and transcript output are bounded. Scratch chunks are
  deleted in `finally`; the next extraction cleans owned interrupted WAVs and refuses
  to accumulate eight locked leftovers.
- Preparation and execution timeouts are central configuration. After timeout or
  cancellation, an invocation has two seconds to drain. If native code ignores
  cancellation, its late result is discarded and no other model overlaps it. Queued
  captures fail with `provider-not-stopped` until that invocation finishes. This is an
  explicit in-process limitation; hard termination would require process isolation.

## Verification

The managed suite passes **883 tests**, including **25 new cases**: 17 worker,
6 execution-store, and 2 real local-source tests. Cases cover FIFO/fallback/language
selection, empty success, content refusal, crashes around publication, restart without
rerunning committed steps, source mutation, retained provenance, superseded requests,
admission racing clear, failed cleanup, revocation during inference followed by
reenabling, throwing cancellation callbacks, and uncooperative late results.

Release solution builds passed on x64 and ARM64 with zero warnings/errors. The
final managed coverage is **93.13%**, above the repository's 90% gate, with zero test
failures or skips. The full x64 application published successfully with Native AOT.
The provider harness published with Native AOT for both x64 and ARM64, retaining
only the four vendor warnings described below. The ARM64 cross-publish needed
`-p:BuildInParallel=false -p:ProduceReferenceAssembly=false -m:1` after the compiler
rejected a generated ARM64 reference assembly; this is a local build workaround,
not an application setting change.

Reproduction commands (run builds sequentially because they share intermediates):

```powershell
dotnet build CaptureTool.slnx -c Release -p:Platform=x64 --nologo -m:1
dotnet build CaptureTool.slnx -c Release -p:Platform=ARM64 --nologo -m:1
dotnet-coverage collect 'powershell -NoProfile -ExecutionPolicy Bypass -File .github\scripts\run-managed-tests.ps1' -s .github/coverage.runsettings -f cobertura -o artifacts/capture-analysis-execution/coverage.cobertura.xml
dotnet publish src/CaptureTool.Presentation.Windows.WinUI/CaptureTool.Presentation.Windows.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:EnableMsixTooling=false -o artifacts/capture-analysis-execution/app-aot-x64 -m:1
dotnet publish tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj -c Release -p:Platform=ARM64 -r win-arm64 -p:PublishAot=true -p:BuildInParallel=false -p:ProduceReferenceAssembly=false -o artifacts/capture-analysis-execution/aot-arm64 -m:1
```

The [smoke instructions](../tools/CaptureTool.Analysis.Smoke/README.md) give the x64
Native AOT publish and packaged run commands.

The separate x64 test package ran the production providers in Native AOT with package
identity. Using synthetic fixtures of roughly 32 seconds:

| Path | Result |
| --- | --- |
| Legacy image OCR | Success, 5 recognized words |
| Legacy sampled-video OCR | Success, 35 recognized words |
| Nemotron multilingual audio and video | Success, 3 segments each; actual model `nemotron-3.5-asr-streaming-0.6b-generic-cpu:3` |
| Whisper audio and video | Success, 3 segments each; actual model `openai-whisper-tiny-generic-cpu:4` |
| Native missing-file transcription error | Rejected through the SDK error path |
| Malformed image | `InvalidSource` |
| Windows AI OCR and image description | `TemporarilyUnavailable` on this machine; inference not verified |

The test package was removed after execution and decoded scratch was empty. Reports,
coverage, fixtures, caches and build logs live in ignored
`artifacts/capture-analysis-execution`. ARM64 hardware execution and available Windows
AI models remain hardware verification gaps. Full application settings/UI and release
MSIX installation flows remain in later slices.

## Native AOT compatibility exception

Foundry 1.2.4 transitively references Betalgo 9.1.0. Its error-message converter emits
four AOT/trim diagnostics (IL2026 and IL3050, in Read and Write). Native AOT does not
apply external link-attribute suppressions. The two executable boundaries import
[NativeAotCompatibility.props](../src/CaptureTool.Infrastructure.Analysis.Windows/Foundry/NativeAotCompatibility.props),
which leaves detailed diagnostics visible and makes these two diagnostic codes
warnings instead of errors. This is a code-level exception, not a method-scoped one;
future publish output must be checked for additional occurrences. Other warning
classes retain the repository's error policy.

The app never serializes SDK responses. Array-shaped SDK error messages can fail to
deserialize in AOT; that becomes a failed model attempt, never successful metadata.
Responses explicitly reporting failure are rejected even if they contain text. Real
successful inference and a native command error were exercised in the AOT package.
Revisit this exception when upgrading Foundry/Betalgo; it is not a claim that every
API in those packages is AOT compatible.

References: [pinned Foundry package](https://www.nuget.org/packages/Microsoft.AI.Foundry.Local.WinML/1.2.4),
[Foundry Windows requirements](https://learn.microsoft.com/en-us/windows/ai/foundry-local/get-started),
[SDK audio implementation](https://github.com/microsoft/Foundry-Local/blob/main/sdk/cs/src/OpenAI/AudioTranscriptionRequestResponseTypes.cs),
[Whisper language tokens](https://github.com/openai/whisper/blob/main/whisper/tokenizer.py),
[pinned error converter](https://github.com/betalgo/openai/blob/a7607aa6a7029d729b2393fceb56327b237be8d0/OpenAI.SDK/ObjectModels/ResponseModels/BaseResponse.cs).

## Slice 3 handoff

Supply a durable `IAnalysisAuthorization`: scanning/consent default off, revision
preserved across restart, rotated on disable/revoke, persisted before reporting the
transition, with revocation cancellation. Start and stop the singleton worker with
the application lifetime. Settings/navigation only observe it.

Capture reconciliation needs a monotonic durable catalog watermark. Pass its current
boundary to `ClearAsync(long)` so older captures are recreated only by an explicit
scan. Slice 2 persists that value with the new generation; it does not invent catalog
ordering. The old no-watermark clear uses `long.MaxValue` to disable automatic
historical reconciliation conservatively. Never turn pending work into a fresh
generation/request automatically.

Stop for review here before implementing slice 3.

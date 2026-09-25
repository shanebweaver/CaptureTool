# Provider smoke checks

This opt-in Windows harness exercises the production adapters using a generated
text image, colored shapes, and synthetic speech/video. It never reads the user's captures or
enables background scanning. Reports contain statuses, counts, model identity,
and diagnostics from synthetic failures. The vision and audio checks also record
descriptions of the generated shapes and synthetic transcripts for inspection.

From the repository root, publish and run on an x64 Windows 11 24H2+ machine:

```powershell
dotnet publish tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj -c Release -p:Platform=x64 -r win-x64 -o artifacts/capture-analysis-execution/aot
& tools/CaptureTool.Analysis.Smoke/run-smoke.ps1 -BinaryDirectory artifacts/capture-analysis-execution/aot -OutputDirectory artifacts/capture-analysis-execution/smoke -Packaged -PrepareAll
```

Release publishing enables Native AOT in the harness project. Do not pass
`-p:PublishAot=true` globally: it also reaches the netstandard build-time generator,
which cannot be published with AOT. Use `-p:PublishAot=false` for a managed publish.

`-Packaged` requires development package registration to be allowed. The script
registers the separate `CaptureTool.AnalysisSmoke.Slice2` identity, verifies it in
the process, and removes it afterward. It does not register or replace CaptureTool.
Use a publish directory without an existing AppxManifest.xml. The script's package
manifest architecture is read from the executable. Publish with
`-p:Platform=ARM64 -r win-arm64` and use the same script on an ARM64 host for native
ARM64 checks. Reports include OS and process architecture; x64 emulation is not
native ARM64 evidence. The script detects an exited/crashed process and cleans up
its own package registration on failure as well as success.

Omit `-PrepareAll` for passive probes; use `-PrepareWhisper` for the small speech
fallback only, or `-PrepareVision` for the Qwen 3.5 0.8B CPU description fallback
(about 1 GB on first use). Preparation can download models into the isolated output directory.
The fourfold synthetic speech fixture spans multiple 15-second chunks. The harness
also verifies malformed-image rejection and a real native transcription failure.
The Qwen checks require a description containing both colors, preserve the video
frame timestamp, and exercise listener start/stop and model load/unload twice.
Unavailable models are reported; they are not treated as successful inference.
`results.json` and `exit-code.txt` are written under the output directory. Scratch
WAV chunks should be gone after execution. Model caches are intentionally retained
for subsequent smoke runs.

The speech matrix checks digital silence, video without an audio track, and German
and French speech generated with installed SAPI desktop voices. It checks an expected
word, timestamps, actual provider identity, unchanged sources, and released scratch.
Missing voices are reported as `MissingSyntheticVoice`; unavailable/unprepared
providers are recorded without claiming successful inference. These synthetic
fixtures establish basic compatibility, not transcription accuracy on real recordings.

For process interruption and library measurements, invoke the published executable
directly; these modes do not initialize or download models:

```powershell
& artifacts/capture-analysis-execution/aot/CaptureTool.Analysis.Smoke.exe "$PWD/artifacts/recovery" --recovery-checks
& artifacts/capture-analysis-execution/aot/CaptureTool.Analysis.Smoke.exe "$PWD/artifacts/scale" --scale-checks
```

Recovery checks kill child processes at ten controlled boundaries, restart them,
and verify durable intent, preserved commits, deletion fencing, encrypted storage,
and orphan temporary cleanup. They use the production worker, catalog, policy,
atomic store, and Windows DPAPI with synthetic analyzers. The publication boundary
injects a partial encrypted temporary write before replacement. This is process
crash evidence, not power-loss or real model interruption evidence.

Metadata enrichment checks use synthetic OCR/transcript entries rather than media:

```powershell
& artifacts/capture-analysis-execution/aot/CaptureTool.Analysis.Smoke.exe "$PWD/artifacts/enrichment" --enrichment-checks
```

The configured text model is prepared if necessary. `enrichment-results.json` retains
normalized outputs, source evidence, real model identities, and fixture acceptance
separately from provider outcomes. Noise/hostile fixtures may safely abstain or be
rejected; a provider failure is never relabeled successful inference. Inspect meaning
as well as the automated schema/category checks. The corpus covers three media kinds,
German, conflicting entries, and a whole oversized entry omitted with explicit coverage.
Use `--enrichment-first` for the invoice only, or `--enrichment-model <catalog-alias>`
to evaluate a candidate without editing production configuration. Models and runtime
components are retained under this output directory for repeat checks.

An opt-in `--enrichment-probe --enrichment-model <catalog-alias>` diagnostic writes
raw responses for one hard-coded synthetic invoice only. It never reads captures.
Production adapters do not log source text, prompts, or raw responses.

Scale checks seed 100, 1,000, and 10,000 DPAPI-protected records with 200 OCR regions
each. Reports measure discovery, settings contention, cancellation, deletion,
catalog reads, managed allocations, and peak working set. They retain isolated
fixtures and JSON reports for inspection. No user capture data is accessed.

The CI diagnostic check can also be run locally:

```powershell
pwsh -NoProfile -File .github/scripts/test-native-aot-log.ps1
pwsh -NoProfile -File .github/scripts/verify-native-aot.ps1 -Platform x64
pwsh -NoProfile -File .github/scripts/verify-native-aot.ps1 -Platform ARM64
```

The latter publishes both app and harness and retains logs. Cross-publishing ARM64
proves compilation only; run the ARM64 harness on an ARM64 device for runtime evidence.

Native AOT currently reports four vendor converter warnings; see the compatibility
exception and verification limits in [review notes](../../docs/capture-analysis-execution-review.md).

# Provider smoke checks

This opt-in Windows harness exercises the production adapters using a generated
text image, colored shapes, and synthetic speech/video. It never reads the user's captures or
enables background scanning. Reports contain statuses, counts, model identity,
and diagnostics from synthetic failures. The vision checks also record the
description of the generated shapes for inspection.

From the repository root, publish and run on an x64 Windows 11 24H2+ machine:

```powershell
dotnet publish tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj -c Release -p:Platform=x64 -r win-x64 -p:PublishAot=true -o artifacts/capture-analysis-execution/aot
& tools/CaptureTool.Analysis.Smoke/run-smoke.ps1 -BinaryDirectory artifacts/capture-analysis-execution/aot -OutputDirectory artifacts/capture-analysis-execution/smoke -Packaged -PrepareAll
```

`-Packaged` requires development package registration to be allowed. The script
registers the separate `CaptureTool.AnalysisSmoke.Slice2` identity, verifies it in
the process, and removes it afterward. It does not register or replace CaptureTool.
Use a publish directory without an existing AppxManifest.xml. The script's package
manifest is x64; ARM64 native publishing can be checked separately with
`-p:Platform=ARM64 -r win-arm64`, but ARM64 runtime checks require an ARM64 host and
the corresponding manifest architecture.

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

Native AOT currently reports four vendor converter warnings; see the compatibility
exception and verification limits in [review notes](../../docs/capture-analysis-execution-review.md).

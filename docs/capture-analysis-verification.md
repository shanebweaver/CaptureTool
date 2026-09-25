# Capture analysis — slice 4 verification

Status: In progress, 2026-09-24. Do not treat this report as release approval.
The reviewed integration was merged into `codex/capture-analysis-core` at
`bff28c4d`; verification continues on `codex/capture-analysis-verification`.

## Environment and outstanding device evidence

Local host: x64 Intel Core i9-9900K, Windows 11 Pro Insider Preview 10.0.26340,
.NET SDK 10.0.401. No additional device is available. Native ARM64 execution and
available Copilot+ Windows AI OCR/description inference remain unverified.
ARM64 cross-publishing and an `Unsupported` result cannot close those gates.
This Insider host also does not establish behavior across supported retail OS versions.

## Fixes established by verification

- Process termination during encrypted publication left a recognized temporary
  file in the current generation indefinitely. Initialization now removes those
  store-owned leftovers under the publication gate, preserves committed records
  and unrelated files, and reports locked leftovers for retry.
- Queue discovery held the storage gate across the full library. It now releases
  the gate every 16 documents and checks the generation again before continuing.
  Clear can interrupt discovery and causes the old results to be discarded.
  All documents still receive full schema/payload validation.
- CI now publishes app and harness for x64 and ARM64, retains logs, and rejects
  unexpected diagnostics. Only IL2026/IL3050 originating in Betalgo's exact
  `Error.MessageConverter.Read` / `Write` methods are accepted. Eleven guard
  regression cases cover allowed, unrelated, missing, empty, and failed logs.
- Unpackaged publishing omitted the app PRI, compiled XAML, fonts, and images.
  Both managed and Native AOT published apps consequently failed to open the
  main window. The app now includes its own UI resources in unpackaged publish
  output; the CI script also checks essential resources. MSIX keeps its existing
  packaging output groups. This was a resource deployment defect, not trimming.
- MSIX received both Foundry's bundled WinML 2.1.1 and the Windows App SDK's
  WinML 2.1.74 DLL. Packaging now excludes only the older Foundry copy when the
  SDK copy is present. Published app/harness hashes already matched the newer
  DLL used in the successful runtime checks; no provider binary was substituted
  without testing.
- Whisper generated `[music]` for an all-zero audio fixture. Both speech adapters
  now skip exactly silent normalized PCM chunks. No amplitude threshold suppresses
  quiet speech, and metadata remains a successful empty transcript.
- Nemotron lost utterance tails when the SDK received one-second PCM pushes.
  Direct SDK diagnostics reproduced truncation and showed complete German speech
  with 100 ms blocks. The streaming adapter now uses that bounded block size;
  it still awaits stop and consumes only final responses. No source padding or
  provisional transcript is published. Fixture assertions check words near the end.

## Stable dependencies

Updated the Windows App SDK from 2.2.0 to 2.5.1 and its AI package from 2.2.3 to
2.5.5, the versions paired by the stable SDK. Microsoft's
[servicing policy](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels)
requires current patches for support; the
[2.5.1 package](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1)
specifies AI 2.5.5. See the
[release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0?pivots=stable).
Foundry Local WinML remains 1.2.4. Its transitive Betalgo 9.1.0 warning exception
remains scoped as documented in the [execution review](capture-analysis-execution-review.md#native-aot-compatibility-exception).

## Automated results

Commands run from the repository root; detailed outputs are under ignored
`artifacts/slice4/` and are not committed source files.

| Check | Evidence | Result |
| --- | --- | --- |
| Release x64 solution build with SDK update | `dotnet build CaptureTool.slnx -c Release -p:Platform=x64 -m:1`; `sdk-update-build.log` | Passed, zero warnings/errors |
| Complete managed suite | `dotnet-coverage collect 'powershell -NoProfile -ExecutionPolicy Bypass -File .github\scripts\run-managed-tests.ps1' -s .github/coverage.runsettings -f cobertura -o artifacts/slice4/final-coverage.cobertura.xml`; `final-managed-tests.log` | 1,004 passed, zero skipped; 94.35% line coverage (90% required) |
| Storage and execution regressions | `discovery-fix-tests.log` | 64 passed; both new regressions failed before their fixes |
| Diagnostic guard | `pwsh -NoProfile -File .github/scripts/test-native-aot-log.ps1` | 11 passed |
| x64/ARM64 Native AOT app and harness | `pwsh -NoProfile -File .github/scripts/verify-native-aot.ps1 -Platform <x64 or ARM64> -OutputDirectory artifacts/slice4/verified-native-aot`; corresponding `verified-native-aot-*.log` | All four publishes passed; four accepted vendor diagnostics per executable; app resources present |
| Combined x64/ARM64 Native AOT MSIX bundle | `package-final.log`, `package-payload-results.json`, `package-*-pri.xml` | Release 2.16.3.0 built; eight accepted vendor warnings, zero errors; both native PE architectures, one expected WinML DLL per package, fonts/images and embedded compiled XAML verified |
| Process interruption, managed harness | `recovery-fixed/recovery-results.json` | Seven passed: registration, admission, preparation, execution, publication, committed step, deletion |
| Process interruption, Native AOT | `recovery-aot/recovery-results.json` | Same seven stages passed with actual child process termination/restart |
| Managed published desktop flows after resource fix | `resource-fix-ui.log` | Settings/consent/progress/deletion and interactive OCR flows passed |
| Keyboard and passive progress | `keyboard-ui.log` | Tab order, keyboard consent revocation, accessible consent name, and nonfocusable progress passed |
| Native AOT desktop flows | `verified-native-ui.log`, final `final-native-ui-diagnostic.log` | Both settings and OCR flows passed, including keyboard checks and receipt of the progress live-region event through UI Automation |
| Packaged x64 Native AOT providers | `verified-provider-smoke.log`, `provider-smoke/results.json` | 23 successful cases; four unavailable Windows AI cases explicitly unverified |
| Empty model cache, passive probes | `passive-smoke/results.json` | Exit 0; Foundry reported preparation required without creating `data/AnalysisModels` or initializing/downloading models |
| Dependency advisory audit | `dotnet list src/CaptureTool.Presentation.Windows.WinUI/CaptureTool.Presentation.Windows.WinUI.csproj package --vulnerable --include-transitive --no-restore`; `dependency-audit.log` | No known vulnerable packages from the current NuGet source |

Recovery uses real child process termination and restart with production DPAPI,
policy, catalog, worker, and storage; analyzers are synthetic. It verifies same-run
recovery, no repeated committed step, deletion fencing, explicit reanalysis after
clear, unchanged source, protected derived data, orphan cleanup, and settled progress.
It does not prove power-loss durability or termination inside a native model.

Settings light/dark and narrow screenshots were visually inspected under
`tests/CaptureTool.UiTests/TestResults/artifacts/capture-memory/`. Consent, scanning,
analysis, and deletion rows remained readable and aligned. All six supported
resource files contain the same 24 nonempty `CaptureMemory_` keys. This is resource
coverage; spoken screen-reader announcements and all translated layouts still
need their own checks.
The final desktop run used the frozen `verified-native-aot/x64/app` executable
and freshly compiled tests with `-m:1 -p:BuildProjectReferences=false`
`-p:UseSharedCompilation=false --no-restore`; earlier build attempts stalled
before test execution. This final run completed with zero warnings/errors.

The MSIX build used Visual Studio 18 Community MSBuild against the app project:
`/restore /m:1 /p:Configuration=Release /p:Platform=x64`
`/p:SolutionDir=D:\Git\CaptureTool\ /p:GenerateAppxPackageOnBuild=true`
`/p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always`
`"/p:AppxBundlePlatforms=x64|arm64"`
`/p:AppxPackageDir=D:\Git\CaptureTool\artifacts\slice4\package\`
`/p:GenerateTemporaryStoreCertificate=false /p:AppxAutoIncrementPackageRevision=false`.
Both executables have no CLR directory. Their WinML SHA-256 hashes match the
Windows App SDK runtime assets (x64 `BBBB34415D8CE303F8A2A2C524C46BD749E21423CCABEB7D38C5FC7334A7848F`,
ARM64 `4FA3283776CF65FD5838A1C3F821CA5282D06B85DA772F9DD2ECC2D514A170DC`).
The app package was inspected without replacing the installed CaptureTool.
The new hosted CI workflow has not yet run.

The packaged provider run used real Windows OCR, QR, file details, Qwen
`qwen3.5-0.8b-generic-cpu:3`, Nemotron
`nemotron-3.5-asr-streaming-0.6b-generic-cpu:3`, and Whisper
`openai-whisper-tiny-generic-cpu:4`. Checks include image/video descriptions,
multi-chunk English speech, German/French speech, silence, no-audio video,
timestamps, unchanged source bytes, scratch release, malformed images, and a
native command error. Only generated fixtures are logged. Successful synthetic
phrases do not establish general transcription accuracy; Whisper's French output
still contained a minor recognition error. Windows AI OCR/description returned
`TemporarilyUnavailable` on this host; this is not inference coverage.

## Large library measurement

Command: invoke the published harness with an absolute isolated output directory
and `--scale-checks`. Fixtures contain 200 OCR regions per protected document.
Reports: `scale-before/scale-results.json`, `scale-after/scale-results.json`.
Single local observations, not timing guarantees; no fixed benchmark threshold.

| Captures | Discovery before / after | Settings wait before / after | Cancellation after |
| --- | --- | --- | --- |
| 100 | 127 / 123 ms | 103 / 18 ms | 1.3 ms |
| 1,000 | 1,014 / 1,028 ms | 670 / 14 ms | 0.8 ms |
| 10,000 | 6,627 / 6,854 ms | 6,559 / 56 ms | 0.9 ms |

At 10,000 records deletion during discovery improved from 7,552 to 876 ms;
catalog reading took 49 ms after the change. Discovery allocated approximately
1.83 GB cumulatively while process peak working set was 109 MB. Discovery remains
linear and runs in the background. The bounded gate removes the measured settings
stall without adding a persistent index; startup-to-first-analysis still includes
the full discovery cost and is a known scaling limit.

## Remaining verification

- Spoken screen-reader output and translated layouts beyond resource-key
  completeness. UI Automation event delivery passed; it does not prove Narrator's
  spoken behavior. Check each supported language at a narrow window width and with
  keyboard focus, and verify the passive progress message using a screen reader.
- Offline preparation with network access actually unavailable, for both a fresh
  isolated model cache and a previously prepared cache. Retain provider status,
  recovery after reconnection, and confirmation that consent/settings stay usable;
  a passive preparation-required result does not establish this behavior.
- Native ARM64 and available Windows AI inference on a capable device. Run the
  [provider harness](../tools/CaptureTool.Analysis.Smoke/README.md) natively with
  matching architecture and retain its OS/process architecture and model reports.

The managed suite includes real Windows decoding, QR and file-details tests plus
worker/consent/storage/OCR state tests. That coverage is complementary to the
remaining real-provider, manual desktop, and device checks, not a substitute.

# Capture analysis: slice 3 review

Implemented on `codex/capture-analysis-integration`, based on `fede19dd` after
merging the reviewed execution branch into `codex/capture-analysis-core`.
`main` is unchanged. Slice 4 remains a separate review/release-verification step.

The [shared consent and OCR follow-up](ai-consent-and-ocr-integration.md) extends
this baseline: existing OCR can reuse saved metadata, and one protected consent
now covers all local AI features. Automatic scanning retains its separate toggle.

## Behavior

Image, audio, and video completion register the retained original and the consent
revision observed at completion. Auto-save updates the preferred path on the same
catalog identity. Capture success does not depend on model or metadata availability.
Existing-capture analysis includes known captured-origin recent entries whose
sources still exist; it does not import arbitrary files, opened-origin history,
or prototype metadata.

Capture Memory starts disabled. Enabling without consent asks for generic consent,
then offers analysis of existing captures. Declining the second prompt leaves
automatic scanning enabled for new captures. Granting consent alone does not enable
scanning. Disabling or revoking consent stops admission immediately, persists the
new revision, and offers deletion when data exists. Declining deletion retains data.

The four settings controls are scanning, consent, analyze existing captures, and
delete analysis information. Analyze existing is disabled while scanning is off.
The passive snackbar contains the existing AI indicator and localized preparation
or analysis text; it disappears on completion, unavailability, or revocation.
There are no new editor analysis panels or Home search results.

### Delete always means delete all

Every confirmed Delete action clears **all** metadata then present, including
metadata written after a previous deletion failed. Its label and meaning never
change. The button is disabled while the deletion operation is running. If deletion
fails and data remains, the app reports an error and enables the button again.
Cancellation of the confirmation leaves the metadata intact. Capture files,
catalog identities, consent, and the scanning preference are preserved.

The existing protected store first commits a new generation, fencing every prior
run, and then removes old generation directories. Startup can finish physical
removal of those already-invalidated files. This internal storage maintenance does
not replace or change the Delete command.

## Integration boundaries

- `CaptureMemoryService` owns policy transitions, prompt sequencing, catalog intake,
  bounded backfill admission, reconciliation, and the singleton runner lifetime.
  Prompts do not hold the command gate. A newer command invalidates stale dialog
  answers and unfinished backfill continuations.
- `CaptureMemoryAuthorization` serializes protected policy changes with the worker's
  admission/publication leases. Disable/revoke blocks immediately, even while an
  older enable write is completing. Failed writes keep the session blocked and
  report an error; an explicit successful enable retry establishes a new revision.
- The protected policy file contains consent, scanning, revision, and the enable
  boundary together. It is separate from deletable analysis storage. Unreadable
  consent prevents scanning but does not hide the metadata deletion action.
- Catalog v2 adds an atomic registration sequence and original automatic-analysis
  authorization. Known v1 identities migrate as historical entries with no automatic
  authorization. Replayed registration cannot change eligibility or erase a saved
  alias. Clear uses the catalog watermark to prevent startup from recreating data.
- Worker admission requires the request's expected authorization revision. Reanalysis
  replaces only the expected prior run and cancels that run's active attempt. Existing
  generation/run tokens still reject late publication after replacement or clear.
- Startup only admits eligible registrations that never reached the queue; it does
  not retry terminal runs or declined historical work. The owner observes one runner,
  restarts the same worker after explicit storage recovery, and awaits shutdown.
- Settings and shell share a single view model. UI updates coalesce on the dispatcher;
  repeated failures across queued captures are deduplicated. Pages do not own workers.
  Progress has a stable accessible text element and throttled polite announcements.

The central configuration and normalized protected metadata ports from slices 1–2
remain the integration points for future models and consuming application features.

## Local description fallback and follow-up review

The configured image and video description steps now prefer Windows AI and fall
back to Qwen 3.5 0.8B through the existing stable Foundry Local WinML 1.2.4 package.
The catalog resolved `qwen3.5-0.8b-generic-cpu:3` on this device. CPU selection
avoids requiring an NPU or GPU; the initial download is about 1 GB. No package
upgrade, new application queue, per-model consent, or model settings were needed.
The new adapter supports images and sampled video frames through the existing
bounded decoder, and publishes `DescriptionMetadata` with actual model provenance.
Image/video plan versions advance to v2. Existing terminal captures are not
silently rescanned; **Run analysis on existing captures** opts into the new plan.

Vision uses the SDK's documented local Responses endpoint because its native chat
wrapper cannot serialize image content with the source-generated context in 1.2.4.
See Microsoft's [vision sample](https://github.com/microsoft/Foundry-Local/blob/main/samples/cs/foundry-local-web-server-responses-vision/Program.cs).
The listener binds only an OS-assigned `127.0.0.1` port for the duration of one
provider invocation. Requests disable response storage, proxies, and redirects;
JPEG bytes stay in memory. Parsing is bounded and accepts only completed assistant
text, excluding reasoning/tool output and rejecting refusals, missing text, model
mismatches, and incomplete responses. Metadata still goes through the protected
store. If cancellation arrives during HTTP inference, the adapter waits for native
completion before unloading; the existing worker fence rejects late output and
prevents another inference from overlapping it.

The follow-up review reproduced and fixed a policy persistence race: a queued
disable or consent revocation could be discarded by a later metadata command or
declined consent dialog. The session stopped scanning, but the durable policy
could remain enabled across restart. Denials now persist unless superseded by a
newer accepted grant. A later enable reads consent after queued policy writes,
so it cannot skip a required prompt using a stale consent value. Regression tests
cover deletion, backfill, declined consent/enable, another disable, restart, and a
newer accepted enable. The Delete command retains its single delete-all meaning.
No further slice 3 blockers were identified; the remaining work is the device and
release verification already assigned to slice 4.

The production adapter described a generated red square and blue circle correctly
for both image and video on this machine, where Windows AI description reported
`TemporarilyUnavailable`. Legacy Windows OCR also succeeded. These are synthetic
device checks, not a claim about description quality on arbitrary captures.

## Validation

The complete managed suite passes **929 tests** without failures or skips. Coverage
is **93.66%** (7,081 / 7,560 lines), above the repository's 90% gate. Added checks use
the real worker, protected metadata/catalog/policy implementations, and deterministic
analyzers. They cover consent transitions, delayed intake, registration recovery,
clear boundaries, interrupted backfill, policy-write races/failures, unreadable
policy/metadata, deletion during failures, catalog migration, and runner recovery.
Existing postprocessor tests now verify intake and auto-save aliases for all three
media types. View-model tests cover dispatcher ordering, command availability,
disposal, and error deduplication.

The slice 3 baseline desktop workflow passes consent grant/decline, backfill, visible passive
progress and disappearance, disable with retained metadata, explicit deletion,
consent revocation, navigation, and restoring the toggle after a cancelled prompt.
Screenshots were inspected at normal and narrow widths in light and dark themes. It uses separate app data
and deterministic test providers; it does not download or invoke production models.
All six supported locales contain the same 24 new nonempty resource keys.

The x64 and ARM64 Release solution builds pass with zero warnings/errors. The integrated app
also publishes successfully with x64 Native AOT. Its only warnings are the four
previously reviewed Betalgo `Error.MessageConverter.Read`/`Write` IL2026/IL3050
diagnostics; no new compatibility warnings appeared. The packaged x64 Native AOT
provider harness additionally passes real Qwen image/video inference, actual model
provenance, video timestamps, and repeated service/model cleanup. Its shape
descriptions matched the managed harness. ARM64 inference and representative media
quality still require slice 4 device verification. Exact validation
commands and ignored evidence:

```powershell
dotnet-coverage collect 'powershell -NoProfile -ExecutionPolicy Bypass -File .github\scripts\run-managed-tests.ps1' -s .github/coverage.runsettings -f cobertura -o artifacts/capture-analysis-execution/slice3-vision-coverage.cobertura.xml
dotnet build CaptureTool.slnx -c Release -p:Platform=x64 --nologo -m:1
dotnet build CaptureTool.slnx -c Release -p:Platform=ARM64 --nologo -m:1
$env:CAPTURETOOL_RUN_UI_TESTS='1'; $env:CONFIGURATION='Release'; $env:PLATFORM='x64'
dotnet test tests/CaptureTool.UiTests/CaptureTool.UiTests.csproj -p:Platform=x64 -c Release --filter FullyQualifiedName~CaptureMemory_ --nologo
dotnet publish src/CaptureTool.Presentation.Windows.WinUI/CaptureTool.Presentation.Windows.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:EnableMsixTooling=false -o artifacts/capture-analysis-execution/slice3-vision-app-aot-x64 -m:1
dotnet publish tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj -c Release -p:Platform=x64 -r win-x64 -p:PublishAot=true -o artifacts/capture-analysis-execution/slice3-vision-aot -m:1
& tools/CaptureTool.Analysis.Smoke/run-smoke.ps1 -BinaryDirectory artifacts/capture-analysis-execution/slice3-vision-aot -OutputDirectory D:/Git/CaptureTool/artifacts/capture-analysis-execution/slice3-vision-smoke -Packaged -PrepareVision
```

Logs use the `slice3-` prefix in `artifacts/capture-analysis-execution/`. UI artifacts
are under `tests/CaptureTool.UiTests/TestResults/artifacts/capture-memory/`.
The fallback and follow-up review use `slice3-vision-` logs; the provider report is
`slice3-vision-smoke/results.json`. Baseline UI checks were not rerun for this
provider/policy-only follow-up.

## Remaining release verification

The [slice 4 PRD](prd-capture-analysis-4-verification.md) still owns the Native AOT
warning CI guard, packaged/hardware coverage, actual ARM64 inference, remaining
Windows AI capability paths, representative real media, and process-interruption
checks. This slice's deterministic tests do not replace those device/runtime checks.
The previously documented vendor warning exception is unchanged; no dependency or
warning suppression was added to resolve it here.

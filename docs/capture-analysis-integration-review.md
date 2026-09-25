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
dotnet publish tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj -c Release -p:Platform=x64 -r win-x64 -o artifacts/capture-analysis-execution/slice3-vision-aot -m:1
& tools/CaptureTool.Analysis.Smoke/run-smoke.ps1 -BinaryDirectory artifacts/capture-analysis-execution/slice3-vision-aot -OutputDirectory D:/Git/CaptureTool/artifacts/capture-analysis-execution/slice3-vision-smoke -Packaged -PrepareVision
```

Logs use the `slice3-` prefix in `artifacts/capture-analysis-execution/`. UI artifacts
are under `tests/CaptureTool.UiTests/TestResults/artifacts/capture-memory/`.
The fallback and follow-up review use `slice3-vision-` logs; the provider report is
`slice3-vision-smoke/results.json`. Baseline UI checks were not rerun for this
provider/policy-only follow-up.

## Final slice 3 hardening

The review after shared consent, saved OCR/QR reuse, and file-details scanning
confirmed three issues, now fixed:

- A run can finish every step while some capabilities fail. The activity snapshot
  now explicitly reports that distinction without changing the persisted run schema.
  Exhausted failed/temporarily unavailable candidates produce the existing localized
  analysis notice. An unsupported final fallback cannot hide an earlier execution
  failure. Successful fallbacks and ordinary unsupported skips stay quiet; useful
  results from later steps are still retained. The view model preserves failures
  through dispatcher coalescing and deduplicates them across a failing queue.
- Saved OCR/QR documents and older OCR supplemented by a fresh QR scan now include
  decoded QR values in Copy all text. A shared document factory keeps that rule
  consistent with fresh OCR, without changing word boxes or duplicating QR values
  when an already-complete document is reused.
- Audio decoding uses the existing scratch lease store for the whole iterator
  lifetime. Clearing temporary files preserves active chunks and their directory;
  normal completion, cancellation, and disposal remove the file and release the
  lease. An owner-scoped allocation overload prunes interrupted audio output and
  retains the existing eight-artifact bound when cleanup cannot finish. Other
  owners and active leases are preserved, including concurrent lease reservations.

The slice 4 checklist now explicitly covers the shared consent, saved OCR/QR,
file-details, partial-failure, and scratch-lifetime paths. It remains release
verification rather than another feature expansion.

The complete managed suite passes **979 tests**, with **94.28% line coverage**
(7,335 / 7,780), above the 90% gate. Both existing desktop workflows pass. The audio
regression uses a real Windows transcode and the real scratch store, exercises
clearing between chunks and completion/cancellation/disposal, and confirms the
original audio bytes remain intact. Storage tests cover locked-file limits,
restart pruning, and preserving other owners. No models were downloaded or invoked
for this hardening pass.

The app and updated provider harness both publish with x64 Native AOT, each with
only the four accepted Betalgo converter IL2026/IL3050 warnings. Harness Release
publishing now enables AOT in its executable project, avoiding a global flag that
incorrectly reached the netstandard build-time generator. Publish commands above
have been updated for that configuration. This is a build-setting correction;
the existing vendor warning exception is unchanged.

Both x64 and ARM64 Release solution builds pass with zero warnings/errors. The
normal x64 solution output was rebuilt after publishing. Packaged app/device
verification and the automated warning guard remain in slice 4.

Commands and ignored evidence are under `artifacts/slice3-hardening/`:

```powershell
dotnet-coverage collect 'powershell -NoProfile -ExecutionPolicy Bypass -File .github\scripts\run-managed-tests.ps1' -s .github/coverage.runsettings -f cobertura -o artifacts/slice3-hardening/coverage.cobertura.xml
$env:CAPTURETOOL_RUN_UI_TESTS='1'; $env:CONFIGURATION='Release'; $env:PLATFORM='x64'
dotnet test tests/CaptureTool.UiTests/CaptureTool.UiTests.csproj -c Release -p:Platform=x64 --filter 'FullyQualifiedName~CaptureMemory_|FullyQualifiedName~TextExtractionMode'
dotnet build CaptureTool.slnx -m:1 -c Release -p:Platform=ARM64
dotnet publish src/CaptureTool.Presentation.Windows.WinUI/CaptureTool.Presentation.Windows.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:EnableMsixTooling=false -o artifacts/slice3-hardening/app-aot-x64 -m:1
dotnet publish tools/CaptureTool.Analysis.Smoke/CaptureTool.Analysis.Smoke.csproj -c Release -p:Platform=x64 -r win-x64 -o artifacts/slice3-hardening/harness-aot-x64 -m:1
dotnet build CaptureTool.slnx -m:1 -c Release -p:Platform=x64
```

## Slice 4 readiness audit

The follow-up architecture/UX review reproduced four defects in three areas and
corrected them before moving on:

- Historical recents used last activity as capture time. New imports now carry an
  unknown time. Catalog v3 clears inferred dates on older imported entries while
  preserving identity, paths, ordering, and authorization. Legacy file-details
  results without the verification marker expose an unknown capture time while
  retaining every other fact. Explicit reanalysis can restore known catalog dates.
- QR-only saved metadata was discarded if OCR failed, and saved OCR retained glyphs
  inside QR bounds that fresh extraction excluded. One application document builder
  now owns reading order, QR exclusion, and copy text for both paths. Missing OCR
  and missing QR remain independent from successful empty results. The editor keeps
  available results through unavailable/declined/failed supplemental scanning and
  retries incomplete documents. Unknown word bounds stay copyable without hitboxes.
- A recovered settings failure could suppress a later independent error while
  scanning stayed off. Settings operation recovery now resets its own notification
  state, including coalesced updates; failing analysis batches still deduplicate.

The delete setting now says **Delete capture analysis** in all six locales, with
copy covering text, QR codes, and file details. Every confirmed deletion retains
its delete-all semantics, and its action stays disabled throughout deletion.

| Requirement | Architecture and evidence |
| --- | --- |
| Extensible, ordered local analysis | `CaptureAnalysisConfiguration` owns ordered steps, candidates, and budgets; adding an existing-capability model changes its adapter/registration and configuration. Worker tests prove FIFO, fallbacks, empty success, language compatibility, timeout/drain, and non-overlap. |
| Durable secure metadata | One protected per-capture document owns run state and results; source/run/generation checks fence stale publication. Storage tests cover atomic failure, superseded runs, restart, unknown schema, and clear with incomplete cleanup. |
| Finalization and identity | Durable registration precedes admission; stable sequence/boundaries distinguish future captures from history. Integration tests cover delayed callbacks, restart, auto-save aliases, idempotent recents import, and capture-date migration. |
| Shared consent and controls | One persisted local-AI consent plus an independent scanning toggle. Tests cover prompt cancellation, queued revocation, stale dialogs, save failures, enable/backfill, disable/retain, and delete-all recovery. |
| Lifetime and progress | One application service owns the runner; pages observe dispatcher-coalesced snapshots. Tests cover navigation, preparation versus execution, idle/unavailable states, terminal failure delivery, and notification recovery. |
| Existing OCR consumer | Source-hash and edit-revision checks precede reuse; shared assembly handles partial OCR/QR, copy text, and bounds. Tests include revoked consent and late non-cooperative results. |
| Bounded local media work | Existing decoder limits and leased scratch remain in place. Real Windows fixture tests cover QR decoding, file properties, audio scratch cleanup, and cancellation. |
| Scope and presentation | Settings follow existing rows and resource conventions; one consent checkbox and a passive snackbar. No editor analysis pane or Home search UX was introduced. |

No additional framework or persistence layer is justified for slice 3. The one
architectural watchpoint is full-document queue discovery under the store gate:
slice 4 now explicitly measures startup, cancellation, delete, and settings latency
with a large library before deciding whether a small work index is necessary.

Validation for this audit: **1,002 managed tests pass**, with **94.34% line coverage**
(7,361 / 7,803). Both desktop workflows pass. Narrow light/dark screenshots were
inspected; the updated deletion copy wraps cleanly and controls remain aligned.
All six locale resources are valid and nonempty. x64 and ARM64 Release solution
builds have zero warnings/errors. No models were downloaded or invoked in this pass.
The app and provider harness both publish with x64 Native AOT, each with exactly
the four accepted Betalgo converter IL2026/IL3050 warnings and no additional
diagnostics. Their logs are `publish-app-aot.log` and `publish-harness-aot.log`.
Normal x64 solution outputs were restored with a clean build after publishing
(`build-x64-restored.log`). Slice 3 is ready for the release verification in slice 4.

Regression evidence is under `artifacts/slice3-readiness/`. Commands match the
preceding hardening block with that artifact directory; `build-x64.log`,
`build-arm64.log`, `desktop-workflows.log`, `managed-tests.log`, and
`coverage.cobertura.xml` retain the results. Focused regression logs are also kept.

## Remaining release verification

The [slice 4 PRD](prd-capture-analysis-4-verification.md) still owns the Native AOT
warning CI guard, packaged/hardware coverage, actual ARM64 inference, remaining
Windows AI capability paths, representative real media, and process-interruption
checks. This slice's deterministic tests do not replace those device/runtime checks.
The previously documented vendor warning exception is unchanged; no dependency or
warning suppression was added to resolve it here.

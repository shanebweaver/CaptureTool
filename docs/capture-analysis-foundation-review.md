# Capture Analysis foundation: review notes

Date: 2026-09-24. Scope: [slice 1](prd-capture-analysis-1-foundation.md).

## Branches

- `codex/capture-analysis-core` starts at `main` commit `fd938e3a` and contains
  the four PRDs in commit `364c8097`.
- `codex/capture-analysis-foundation` branches from that documentation commit and
  contains the implementation. Review against `codex/capture-analysis-core`.
- Both branches are local. Slice 2 has not started.

## Implementation map

| Concern | Location |
| --- | --- |
| Step/model order, bounded budgets, composition validation | [CaptureAnalysisConfiguration](../src/CaptureTool.Application/Analysis/CaptureAnalysisConfiguration.cs) |
| Provider-neutral availability, preparation, progress, typed outcomes | [Analyzer contract](../src/CaptureTool.Application.Abstractions/Analysis/IMediaAnalyzer.cs) |
| Capture-owned stable identity and source/preferred locations | [CaptureAsset](../src/CaptureTool.Domain.Capture/CaptureAsset.cs), [catalog](../src/CaptureTool.Infrastructure/CaptureAssets/LocalCaptureAssetCatalog.cs) |
| Canonical results, source revision, preserved producer/plan provenance | [Analysis aggregate](../src/CaptureTool.Domain.Analysis/CaptureAnalysisRecord.cs) |
| Query/write boundary and run tokens | [Store ports](../src/CaptureTool.Application.Abstractions/Analysis/ICaptureAnalysisStore.cs) |
| Atomic generation changes and protected metadata | [LocalCaptureAnalysisStore](../src/CaptureTool.Infrastructure/Analysis/Persistence/LocalCaptureAnalysisStore.cs) |
| Serialize/protect before any disk writes | [ProtectedDocumentFile](../src/CaptureTool.Infrastructure/Persistence/ProtectedDocumentFile.cs) |
| Windows current-user protection | [WindowsUserDataProtector](../src/CaptureTool.Infrastructure.Windows/Security/WindowsUserDataProtector.cs) |

The new Analysis domain references only the shared Domain project. Capture and
Analysis have independent domain types; the application owns their translation.
Serialization DTOs are infrastructure-owned and use strict source-generated JSON.
Shared file IO stays outside either domain. The metadata read/write ports resolve
to the same singleton so they share the publication gate.

## Persistence behavior to review

Each run receives a token containing capture identity, source revision, run ID,
and storage generation. Starting a newer run supersedes the previous token.
Results from the same source remain available during refresh and retain the plan
version that produced them. Results for a different source revision are removed
from the current record. Readers can require a matching source revision.

Clear first atomically publishes a fresh generation, then removes old generation
directories under the same short gate. Old tokens fail, and old files cannot be
read through the query API even if deletion is interrupted. Initialization retries
cleanup without touching the active generation. Cleanup failure is explicit and
does not roll back the invalidation. Capture files and the capture catalog are
outside this deletion boundary.

Protection, publication, corrupt-document, and unsupported-schema failures never
become successful empty writes. An interrupted first control write can recover
its recognized unpublished temporary file; missing control alongside an existing
record directory fails closed. Atomic writes protect against process interruption,
not loss of the underlying disk. Stores assume the app's existing single process.

## Verification

The repository's managed regression script passed **848 tests**, with zero failures
or skips: Application 332; Capture Windows 21; Edit Windows 50; Infrastructure 159;
Windows Infrastructure 11; MCP 21; Presentation 254. This includes **33 new tests**.

Meaningful new coverage includes deterministic configuration, descriptor/schema
compatibility, immutable payloads, source/run replacement, retained provenance,
encrypted round-trips of every payload, catalog replay after auto-save, identity
conflicts, failed protection and publication, actual Windows file-lock replacement
failure, cancellation, clear/publication races, cleanup interruption/restart,
missing/corrupt/unknown storage, and interrupted first initialization. Two tests
exercise actual Windows protection and tamper rejection.

Commands run:

```powershell
dotnet build CaptureTool.slnx --configuration Debug -p:Platform=x64 --nologo -m:1
dotnet build CaptureTool.slnx --configuration Release -p:Platform=x64 --nologo -m:1
& .github/scripts/run-managed-tests.ps1
dotnet-coverage collect 'powershell -NoProfile -ExecutionPolicy Bypass -File .github\scripts\run-managed-tests.ps1' -s .github/coverage.runsettings -f cobertura -o artifacts/capture-analysis-foundation/coverage.cobertura.xml
```

Both solution builds completed with zero warnings/errors. The final infrastructure
fix was recompiled and verified by the complete regression/coverage run. Coverage
for the repository's configured modules is **93.24%**, above the **90%** gate. The
new Analysis domain is included in that gate (96.43% line coverage). The generated
report is in the ignored `artifacts/capture-analysis-foundation` directory.

## Slice boundary

Registration adds no scanning, model downloads, or new UI behavior. The default
plans describe the provider registry to implement in slice 2; actual adapters are
not registered yet. Source hashing/snapshots, queue recovery, consent/enabled-state
authorization, capture finalization, and progress/settings integration remain in
their specified later slices. Store tokens are concurrency controls, not user
consent; the later application workflow must enforce consent at admission and
publication.

These checks do not claim real-model execution, ARM64 runtime testing, MSIX install
validation, Native AOT publishing, or UI flow verification. Those remain the
provider/integration/release gates in the other PRDs.

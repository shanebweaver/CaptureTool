# Capture Memory v1 architecture and release checklist

Date: 2026-08-31

## Decision

Capture Memory is a metadata-first subsystem. AI output is committed to protected canonical
metadata before the disposable search projection is updated. Original captures are never the
metadata store and are not modified by analysis.

The v1 pipeline is:

1. stable capture identity and source revision;
2. durable consent/enrollment policy;
3. protected per-capability jobs;
4. protected canonical metadata and user-visible manifest export;
5. disposable, rebuildable search projection.

This feature is unreleased. There is no legacy-data migration requirement. Local development data
may be erased if a schema becomes incompatible; permanent user exclusions must never be inferred
or restored.

## Application-owned workflow

Home and Settings are now clients of one singleton `ICaptureMemoryWorkflow`. Pages do not grant
consent, prepare models, authorize backfill, queue captures, or own cancellation tokens.

Every accepted command is first written to a protected atomic operation journal. Its operation ID,
request, policy epoch, phase, bounded capture IDs, and result contain no source paths or recognized
content. The workflow then performs side effects and advances that journal. Startup resumes an
unfinished command with the same identity. Recovery refuses to re-enable consent after revocation.

Turn-off/erase durably supersedes another command before cancelling it, so revocation does not wait
for a model provider that ignores cancellation. Ordinary overlapping commands are rejected rather
than overwriting active intent. Navigation only unsubscribes from observation.

Reanalysis propagates its operation ID into each durable capability job. A restart can adopt
already-running work and cannot requeue a job completed by the same operation. A later explicit
reanalysis gets a new identity and may intentionally refresh it.

## Lifecycle invariants

- Revocation and clear-memory write policy/enrollment tombstones before derived cleanup.
- Cleanup and enrollment restoration share a short application gate. Cleanup re-reads the durable
  tombstone under that gate, so a queued stale purge cannot remove freshly restored metadata.
- Generation and policy fences remain mandatory at source verification and metadata commit.
- Per-capture privacy exclusions and forgotten captures are not eligible for reanalysis/backfill.
  Global MemoryCleared tombstones are recoverable only through an explicit user action.
- Search publication happens before a job is completed. A query already on Home refreshes when
  projection content changes.
- Model availability is media/capability-specific. Supported OCR or speech work can proceed when a
  different model is unavailable. No preview provider or remote fallback is introduced here.
- The main-window activity indicator derives queued/running/waiting state from durable jobs and
  active preparation from providers. Merely having backfill permission is not treated as activity.

## Manual acceptance checklist

Use a packaged x64 build on a supported device.

- Turn Memory off and erase, then enable with **Include existing captures** from Home. Repeat from
  Settings. Search known text immediately after durable jobs finish; do this three times.
- Start model preparation, navigate Home → Settings → Home, and verify progress remains the same
  operation. Cancel it from Settings and verify controls recover.
- Start preparation with a slow/unavailable provider, choose **Turn off and erase**, and verify
  consent turns off promptly without waiting for that provider.
- Interrupt the app during preparation, existing-capture queueing, reanalysis queueing, analysis,
  search publication, and cleanup. Restart and verify one operation resumes without duplicate work.
- Clear Memory while leaving consent on. Reanalyze; verify only MemoryCleared captures return.
  Explicitly removed/private/forgotten captures must remain absent.
- Keep a search query open during image, video, and speech analysis. Results must appear without
  retyping once projection publication completes.
- On a device missing one model, verify supported media finish and the activity indicator does not
  leave an indefinite “waiting for models” summary solely because an unsupported capability exists.
- Test English and one non-English audio/video capture with language auto-detection. Inspect the
  exported manifest for transcript segments and analyzer identity.
- Exercise a locked/missing original and interrupted cleanup. The UI must show partial/recovery
  status and remain retryable rather than permanently disabling Reanalyze.

## Release gates

- Run all Domain, Application, Infrastructure, Presentation, Windows analyzer, and WinUI tests.
- Build x64 Debug and Release/store configurations, including AOT if used for Store submission.
- Complete the device checklist above on a fully supported machine and a limited-capability machine.
- Verify manifests never expose secrets and protected journals never contain paths or recognized
  content.
- Confirm the shipping model inventory contains no preview/experimental model.

Preparation/queue completion is not recognition completion. The operation journal diagnoses commands;
the durable job store and main-window activity UX diagnose AI work through search readiness.

# PRD: Resumable Capture Analysis and managed artifact lifecycle

- Architecture decision: `ARCH-CAP-25`
- Severity: High
- Status: Implemented
- Affected areas: analyzer contracts, Analysis worker, local persistence, cleanup lifecycle, Windows video analysis

## Summary

Long-running on-device analysis must survive ordinary interruption without converting partial work into canonical metadata or repeatedly starting from the beginning. The Analysis worker will provide each analyzer with an optional, provider-neutral checkpoint scoped to the exact capture bytes, capability schema, and analyzer revision. Checkpoints are protected, atomic, bounded, disposable recovery aids.

Video OCR is the first consumer. It persists enough normalized state every ten representative samples to resume at the exact next media timestamp while preserving time ranges and text coalescing. Successful, stale, terminal, forgotten, revoked, and cleared work removes its checkpoint. Interrupted or transient work may retain it. All derived media and checkpoint deletion is constrained to exact app-managed roots and abandoned artifacts are pruned after seven days.

## Problem

Sampled video OCR and future audio/video models may run for minutes. Process shutdown, cancellation, transient provider failure, or a device interruption currently causes all completed work to be lost. A naive recovery design creates new risks:

1. partial analyzer output can be mistaken for canonical metadata;
2. a checkpoint from old source bytes or old model behavior can corrupt a new run;
3. checkpoints can expose recognized text in plaintext;
4. partially written state can become unreadable after a crash;
5. temporary WAV/frame artifacts can accumulate indefinitely;
6. broad cleanup can delete user content or files outside Analysis ownership;
7. checkpoint failures can make otherwise valid analysis fail.

## Goals

1. Add a minimal provider-neutral checkpoint contract to `CaptureAnalysisRequest`.
2. Scope checkpoint identity to capture, verified source revision, capability definition, and analyzer revision.
3. Protect checkpoint payloads to the current Windows user and write them atomically.
4. Enforce a bounded payload size and discard corrupt or incompatible state safely.
5. Define retention and deletion semantics across every Analysis lifecycle operation.
6. Make video OCR resume at the next unprocessed sample timestamp with equivalent canonical output.
7. Keep checkpoint use optional so short analyzers and existing providers remain simple.
8. Restrict cleanup to app-created, non-user content under exact managed roots.

## Non-goals

- Treating checkpoints as canonical metadata, search input, telemetry, or user documents.
- Resuming a provider's opaque remote server operation.
- Guaranteeing recovery after source bytes, schema, adapter, model, prompt, or configuration changes.
- Persisting decoded video frames.
- Adding parallel video-frame inference.
- Recovering arbitrary scratch files whose ownership predates this design.
- Allowing analyzers to choose checkpoint file paths.

## Checkpoint contract requirements

1. `CaptureAnalysisRequest` always supplies an `ICaptureAnalyzerCheckpoint`; analyzers that do not need it may ignore the no-op implementation.
2. The contract supports reading, atomically replacing, and clearing one opaque byte payload.
3. The Application layer defines checkpoint identity and lifecycle; analyzers never receive or construct storage paths.
4. A checkpoint key includes `CaptureId`, full verified `SourceRevision`, exact `CapabilityDefinition`, and full `AnalyzerRevision`.
5. Empty or incomplete keys are rejected before storage access.
6. The local store derives opaque hashed directory/file names and never places source paths, recognized text, or model names in filenames.
7. A payload larger than 64 MiB is rejected before protection or write.

## Storage and security requirements

1. Payloads are protected with the existing current-user data-protection service.
2. Writes use the existing atomic-file abstraction so interruption leaves either the prior complete payload or the new complete payload.
3. The stored header validates format magic, checkpoint-key binding, and bounded payload length before unprotecting.
4. A corrupt, truncated, mismatched, undecryptable, or incompatible checkpoint is deleted and treated as absent.
5. Read and cleanup failures are logged with bounded messages that contain no payload or user path.
6. Per-checkpoint, per-capture, and global deletion operations are supported.
7. Recursive deletion first proves that the resolved target is within the exact checkpoint root.
8. Store operations serialize conflicting access so reads, writes, pruning, and cleanup cannot race within the process.

## Worker lifecycle requirements

1. The worker opens a checkpoint only after selecting and authorizing an analyzer, using that analyzer's revision.
2. Successful or already-current work clears its checkpoint before completing the durable intent.
3. Stale commit, terminal failure, exhausted retries, unsupported output, invalid output, explicit job cancellation, forget, clear, or consent revocation clears the relevant checkpoint.
4. Process cancellation and classified transient failure preserve the checkpoint for retry.
5. Checkpoint read/write/clear failure cannot convert valid analyzer output into failure or prevent a safe job transition.
6. Worker startup prunes checkpoints whose last write is older than seven days; pruning failure is isolated and does not block startup.
7. Capture deletion and global Analysis cleanup delete checkpoints in the same lifecycle coordination used for jobs, metadata, and projections.

## Video OCR resume requirements

1. The video OCR adapter uses a source-generated, versioned checkpoint payload.
2. State includes the next sample timestamp, all completed normalized observations, and any active text span needed to preserve coalescing semantics.
3. State is saved after every ten successfully processed representative samples and when cancellation interrupts an active run.
4. Resume asks the frame source to begin at the saved timestamp rather than decoding and discarding the prefix in the production Windows implementation.
5. Restored observations and time ranges are validated before use; invalid state is cleared and analysis starts from frame zero.
6. Resumed output is equivalent to uninterrupted output for the same source and analyzer revision.
7. The final canonical result is committed only after all bounded samples complete; checkpoint state is never projected into Capture Memory.

## Managed working artifact requirements

1. Video source copies and extracted audio use a dedicated Analysis working directory below the platform temporary root.
2. Filenames are random app-generated identities and retain only the required extension.
3. Exact working files are deleted in `finally` or delete-on-close paths after use.
4. Cleanup ignores any path whose fully resolved form is outside the dedicated working root.
5. On first use per process, app-created working files older than seven days are pruned best-effort.
6. No cleanup operation enumerates or deletes Capture Assets, auto-save destinations, imported user files, or unrelated temporary folders.

## Reliability and privacy requirements

- A checkpoint is useful only for performance; losing it changes no canonical result semantics.
- A successful analyzer result remains successful even when checkpoint persistence is unavailable.
- Checkpoint state is never logged, indexed, synchronized, or included in crash diagnostics owned by the subsystem.
- Source revision and analyzer revision changes naturally select a different checkpoint key.
- File and directory cleanup is idempotent.
- Retention time is an implementation policy and does not imply user-content retention.
- Native AOT and source-generated serialization remain required for production packages.

## Test plan

Add focused coverage for:

- protected checkpoint round trip and absence of plaintext canary content;
- atomic replacement, per-checkpoint clear, per-capture deletion, and global deletion;
- invalid keys, oversize payloads, corrupt/truncated/key-mismatched data, and non-UTC prune cutoffs;
- pruning old checkpoints while preserving fresh checkpoints;
- worker clearing and preserving checkpoints for every terminal, stale, success, cancellation, and transient path;
- forget/clear/revocation cleanup ordering;
- video OCR checkpoint creation at ten samples, cancellation persistence, resume timestamp, state validation, and output equivalence;
- safe working-path deletion and abandoned-file pruning without touching an external canary file;
- dependency-injection registration and source-generated serialization metadata.

Run the complete managed test suite under the repository coverage command, enforce the 90% line-coverage gate, build WinUI x64, and build the ARM64/x64 Store bundle.

## Acceptance criteria

- [x] Analyzer requests expose an optional provider-neutral checkpoint without storage paths.
- [x] Checkpoints are bound to capture bytes, capability schema, and analyzer revision.
- [x] Checkpoint data is current-user protected, atomic, key-bound, and limited to 64 MiB.
- [x] Corrupt or incompatible checkpoints are discarded without failing valid analysis.
- [x] Success, stale/terminal work, cancellation by the user, forget, clear, and revocation remove applicable checkpoints.
- [x] Process interruption and transient failure preserve resumable state.
- [x] Checkpoints and Analysis-owned working files older than seven days are pruned safely.
- [x] Video OCR checkpoints every ten samples and resumes at the next timestamp with output equivalent to uninterrupted processing.
- [x] Cleanup cannot delete files outside exact Analysis-managed roots or any user content.
- [x] Full managed tests pass at or above 90% line coverage.
- [x] WinUI x64 and ARM64/x64 Native AOT Store builds succeed without warnings or errors attributable to this work.

## Verification

Verified on 2026-08-12:

- the repository CI-managed command passed all 1,326 Release tests across ten test projects;
- Cobertura line coverage was 90.17% (14,923/16,550) and branch coverage was 76.34% (5,092/6,670);
- protected checkpoint tests cover atomic replacement, key binding, corruption, size limits, per-checkpoint/capture/global deletion, and retention pruning;
- video tests cover invalid-state discard, process interruption, timestamp resume, uninterrupted-output equivalence, bounded adaptive sampling, and managed-path deletion safety;
- WinUI x64 Debug built with zero warnings and zero errors;
- ARM64 and x64 Native AOT MSIX packages and the combined Store `.msixupload` bundle were created successfully;
- `git diff --check` reported no whitespace errors.

## Rollout

The checkpoint store is registered with the existing local Analysis infrastructure. Existing jobs require no migration and analyzers may ignore the new contract. Old or incompatible checkpoint files are disposable and can be removed. Video OCR begins checkpointing immediately when its analyzer is enabled; feature and provider flags continue to control whether video analysis runs at all.

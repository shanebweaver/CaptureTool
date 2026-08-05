# PRD: Separate Retained Captures from Leased Scratch Artifacts

- Issue: [#423](https://github.com/shanebweaver/CaptureTool/issues/423)
- Architecture finding: `ARCH-18`
- Severity: Medium
- Status: Implemented
- Affected features: `APP-05`, `APP-06`, `CAP-08`, `IMG-15`, `VID-02`, `VID-03`, `VID-04`, `PLT-02`

## Summary

CaptureTool must store catalog-backed captures separately from disposable edit and export artifacts. Captures that are not auto-saved remain retained application data and survive cache cleanup. Scratch work receives an explicit owner lease, is protected while in use, is deleted when the owning session releases it, and is scavenged after an age threshold when a prior process could not release it cleanly.

## Problem

The application currently writes retained captures, imported working copies, rendered trim previews, clipboard trims, Paint exports, and AI-derived media into the same application temporary folder. The settings command clears every entry in that folder without distinguishing the user's only catalog-backed capture from disposable work or checking whether an operation is still using it.

This creates three failure modes:

1. clearing temporary files can delete a recent capture that has no other saved copy;
2. cleanup can race with an editor, renderer, or AI operation that is actively reading or writing a file;
3. abandoned scratch files accumulate indefinitely unless the user clears the entire shared folder.

## Goals

1. Give retained captures and scratch artifacts distinct, explicit storage locations.
2. Keep non-auto-saved captures available through the recent-capture catalog after cache cleanup.
3. Give each scratch artifact an isolated owner directory and an active lease.
4. Prevent manual cleanup and startup scavenging from deleting active leases.
5. Delete session-owned working copies, previews, Paint exports, and AI derivatives when their owner is disposed.
6. Scavenge abandoned scratch directories after seven days at application startup.
7. Preserve clipboard-backed file artifacts long enough to be pasted, while making them eligible for later cleanup.

## Non-goals

- Changing the recent-capture catalog format or retention policy.
- Moving or deleting the user's configured auto-save destination files.
- Deduplicating retained captures after auto-save.
- Adding a settings control for deleting retained captures.
- Recovering a scratch operation after the process terminates unexpectedly.
- Coordinating leases across multiple CaptureTool processes; the existing single-instance application model remains authoritative.

## Functional requirements

### Storage classification

1. Add an application-local retained-capture folder below the durable application data root.
2. Add an application scratch folder below the platform temporary root.
3. Image, video, and audio capture workflows allocate their initial output in the retained-capture folder.
4. Imported working copies, recent-capture working copies, AI derivatives, rendered trim previews, Paint exports, and trimmed clipboard files allocate in scratch.
5. Settings that display or open the temporary-files folder refer to scratch only.

### Scratch ownership

1. Every scratch allocation receives a unique owner directory so cleanup cannot partially delete another operation's files.
2. A central scratch-artifact store tracks active owner directories for the lifetime of the process.
3. Manual cleanup deletes only unleased scratch directories and files.
4. Releasing a session-owned artifact removes its lease and recursively deletes its owner directory.
5. Relinquishing a clipboard-owned artifact removes its active lease without deleting the file so the flushed clipboard can continue to resolve it.
6. Release operations are idempotent and never delete paths outside the configured scratch root.

### Owner lifecycle

1. Editor working copies are released when the corresponding editor view model is disposed.
2. Image and video AI derivatives are released when replaced, cancelled after allocation, or when the editor is disposed.
3. Rendered video trim previews are released when superseded or when the page is unloaded.
4. Paint exports are released when the image editor is disposed.
5. Scratch allocations are released immediately when navigation or generation fails before ownership transfers to a session.

### Startup scavenging

1. Application startup scans the scratch root once after core services initialize.
2. Unleased owner directories older than seven days are deleted.
3. Fresh and active owner directories are preserved.
4. A failure to inspect or delete one entry is logged and does not prevent startup or cleanup of other entries.

## Reliability requirements

- Capture allocation remains collision-safe.
- Scratch cleanup is best-effort and cannot make application startup fail.
- Manual clear remains best-effort and reports success after attempting every eligible entry.
- A malformed or external path passed to release is ignored safely.
- Cancellation and generation failures do not leave an in-process active lease.
- Retained captures are never enumerated by the scratch clear or scavenging operations.

## User experience

- Clearing temporary files no longer removes unsaved recent captures.
- Active editors and render operations continue working if temporary cleanup runs concurrently.
- Leaving an editor cleans up its disposable working files and derived previews.
- The existing temporary-files folder and clear-cache controls continue to operate, but now expose and clear scratch data only.

## Test plan

Add focused regression coverage for:

- image, video, and audio workflows allocating under retained storage;
- manual cleanup preserving retained captures and active scratch leases;
- released scratch artifacts being deleted without affecting another lease;
- stale startup scratch entries being scavenged while fresh and active entries survive;
- imported and recent-capture working copies transferring ownership to an editor and cleaning up on failure;
- editor disposal releasing working copies and generated derivatives;
- clipboard trim artifacts becoming unleased without immediate deletion;
- explicit storage paths in the Windows and UI-test implementations.

Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Catalog-backed captures without auto-save survive the temporary-files clear command.
- [x] Captures and scratch artifacts use different application storage roots.
- [x] Active scratch artifacts cannot be removed by manual cleanup or startup scavenging.
- [x] Session-owned scratch artifacts are deleted deterministically on owner disposal.
- [x] Abandoned scratch directories older than seven days are removed at startup.
- [x] Clipboard file artifacts remain available after copy and are eligible for later cleanup.
- [x] Cleanup failures remain isolated, logged, and recoverable.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No data migration is required. Existing files already placed directly in the platform temporary folder are left untouched because their ownership cannot be inferred safely. New captures use retained storage and new scratch artifacts use the leased scratch root immediately after upgrade.

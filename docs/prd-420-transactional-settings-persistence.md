# PRD: Transactional Settings Persistence

- Issue: [#420](https://github.com/shanebweaver/CaptureTool/issues/420)
- Architecture finding: `ARCH-15`
- Severity: Medium
- Status: Implemented
- Affected features: `PLT-01`, `PLT-05`, `PLT-06`, `PLT-07`

## Summary

Settings mutations that require persistence must be serialized with their storage write and return a typed result. The in-memory settings dictionary, subscribers, and dependent runtime services must change only after persistence succeeds. Bulk reset must use the same lock and notification discipline as individual settings.

## Problem

Callers currently invoke synchronous `Set`, `Unset`, or `ClearAllSettings` and then separately call `TrySaveAsync`. Most callers discard the Boolean save result and return successful typed responses. A failed write therefore leaves an unpersisted value in memory and the settings UI continues to show the requested state until restart.

The split mutation/save sequence also allows settings operations to interleave. `ClearAllSettings` bypasses the dictionary access lock entirely and does not raise `SettingsChanged`, so concurrent readers can race with reset and subscribers do not refresh after defaults are restored.

## Goals

1. Serialize each persisted mutation and its storage write.
2. Keep the last committed in-memory settings visible until the write succeeds.
3. Return an explicit saved, persistence-failed, or service-unavailable result.
4. Publish settings notifications and telemetry only for committed mutations.
5. Make bulk reset lock-safe and notify subscribers of every removed setting.
6. Propagate persistence failures through settings use-case responses and restore optimistic UI fields.
7. Apply dependent runtime state, including language, logging, AI consent, and telemetry consent, only after persistence succeeds.

## Non-goals

- Changing the JSON settings schema or file location.
- Retrying failed storage writes automatically.
- Adding a settings database or cross-process synchronization.
- Changing theme persistence, which uses the Windows application-data store rather than `ISettingsService`.
- Changing settings defaults.

## Functional requirements

### Transaction API

1. Add typed async operations for setting, unsetting, and clearing persisted settings.
2. Each operation builds a candidate dictionary from the committed state under the access lock.
3. Candidate writes are serialized with initialization and other saves.
4. A successful write commits the candidate in memory, raises `SettingsChanged`, and records safe telemetry.
5. A failed write leaves the committed dictionary and subscribers unchanged and returns `PersistenceFailed`.
6. A disposed service returns `ServiceUnavailable` without accessing storage.

### Bulk reset

1. Transactional reset writes an empty candidate before publishing defaults in memory.
2. Successful reset notifies subscribers with all removed definitions.
3. The legacy synchronous clear operation uses the normal access lock and notification path.
4. Readers continue seeing the prior committed values while a reset write is pending.

### Caller behavior

1. Settings, folder, language, restore-defaults, and logging use cases set their response success from the mutation result.
2. Language and logging runtime state changes only after the settings write succeeds.
3. AI and telemetry consent gates change only after their setting is saved.
4. Optimistic settings-page fields revert to the committed service value when a use case reports failure.
5. Restore-defaults refreshes the page only after reset persistence succeeds.

## Reliability requirements

- Cancellation continues to propagate to callers.
- Storage exceptions remain logged and are represented by `PersistenceFailed`.
- Missing initialization continues to fail explicitly.
- Concurrent readers never enumerate or query a dictionary while it is being cleared without the access lock.
- A later synchronous mutation is not overwritten when an earlier transactional write completes.

## Test plan

Add tests for:

- failed transactional set retaining the committed value and suppressing notifications;
- pending transactional reset keeping prior values readable until the write completes;
- successful reset notifying subscribers with all removed settings;
- disposed transactional mutation returning service unavailable;
- update and restore use cases returning failure when persistence fails;
- dependent language, logging, AI consent, and telemetry state not changing after failed persistence;
- settings-page optimistic values reverting after a failed response.

Run all non-UI test projects and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Persisted settings mutations are serialized and return a typed result.
- [x] Failed writes do not alter committed in-memory settings or notify subscribers.
- [x] Bulk reset is lock-safe, atomic from readers' perspective, and notifies on success.
- [x] Use cases and runtime consent or behavior services do not report or apply failed changes.
- [x] Settings-page optimistic fields revert after persistence failure.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

## Rollout

No migration or feature flag is required. Existing settings files remain compatible; the change only affects how mutations are committed and reported.

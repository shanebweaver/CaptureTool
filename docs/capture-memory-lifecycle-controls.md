# Capture Memory lifecycle controls

Capture Memory lifecycle operations are exposed through a dedicated Settings child view model and remain behind `ICaptureMemoryFeatureAvailability`. The feature stays hidden when either the Capture Analysis platform flag or the Capture Memory search flag is off.

## Layering

- WinUI owns localized rendering, accessibility metadata, and `ContentDialog` confirmations.
- `CaptureMemorySettingsViewModel` coordinates application ports. It never resolves or invokes an analyzer/provider directly.
- `ICaptureAnalysisPolicyCommandService` owns future-admission and revocation transitions.
- `ICaptureAnalysisMaintenanceService` owns clear, projection rebuild, and consent-gated reanalysis.
- `ICaptureAssetRemovalService` owns per-capture tombstones, cleanup, and app-owned retained-source deletion.
- Durable control state is updated before derived-data cleanup, so restart reconciliation can finish an interrupted operation without restoring stale search results.

## User-visible actions

| Action | Metadata/search data | Capture source | AI model work | Future analysis |
| --- | --- | --- | --- | --- |
| Stop analyzing new captures | Preserved | Not read or changed | None | Stopped |
| Resume analyzing new captures | Preserved | Read only after a future capture is admitted | As required for admitted captures | Enabled |
| Turn off and erase | Erased through durable revocation and cleanup | Never deleted | None | Disabled |
| Remove from Memory | Erased for one capture | Never deleted | None | Unchanged |
| Delete capture | Erased for one capture | Deletes only an existing `AppOwned` retained source after confirmation | None | Unchanged |
| Clear Memory | Erased for enrolled captures | Never deleted | None | Remains enabled |
| Rebuild search index | Disposable projection is recreated from metadata | Never read | None | Unchanged |
| Reanalyze captures | Existing metadata may be replaced by newly scheduled analysis | Enrolled, authorized sources are read | Required/available optional models are prepared with progress and cancellation | Unchanged |

The Library result resolver exposes only the derived `CanDeleteRetainedSource` capability. Presentation never infers ownership from a path. Legacy and external captures therefore cannot render the Delete capture action, and the application service checks ownership again before deleting.

Separately saved or exported copies are not deletion targets. Delete capture applies only to the catalogued app-owned retained source.

## Progress, cancellation, and recovery

Reanalysis reports two application-level phases: model preparation and capture scheduling. Cancellation flows through the maintenance port. Completed durable safety transitions remain effective even if UI cancellation arrives while idempotent cleanup is finishing.

Conflict, rejection, unavailable-model/source, incomplete cleanup, and reconciliation-required states are rendered separately. The view model refreshes durable policy after every operation so the controls reflect the post-crash or post-cancellation state rather than optimistic UI state.

## Accessibility and localization

All commands are standard keyboard-invokable WinUI buttons. Status text uses polite live-region announcements, progress and action controls have stable automation IDs, and destructive dialogs do not select a default action. UI and confirmation language is resource-backed in all supported locales.

## Verification

Focused application tests enforce projection-only rebuild behavior, optional-model handling, preparation/scheduling progress, tombstone ordering, ownership checks, and retained-source deletion. Presentation tests cover policy transitions, cancellation, recovery, unavailable models, and confirmed per-result actions. Desktop automation exercises Home removal and the Settings reanalyze, rebuild, stop, resume, clear, and turn-off flows through real WinUI controls and dialogs.

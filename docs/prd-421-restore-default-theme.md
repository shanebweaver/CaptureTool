# PRD: Restore the persisted application theme with Settings defaults

- Issue: [#421](https://github.com/shanebweaver/CaptureTool/issues/421)
- Architecture finding: `ARCH-16`
- Severity: Medium
- Status: Implemented
- Affected features: `PLT-01`, `PLT-03`

## Summary

Restore Defaults must reset the application theme to System Default immediately and remove the separately persisted Windows theme override so the reset survives an application restart.

The theme remains in its early-startup Windows settings store because it must be available before the main settings repository initializes. The theme service will expose an explicit reset operation, and the restore-defaults use case will invoke it as part of the same application action that clears ordinary settings, telemetry consent, and the language override.

## Problem

The application theme is persisted under `themeSetting` in Windows local settings, outside `ISettingsService`. `RestoreDefaultsUseCase` clears only the main settings repository, telemetry consent, and language override. `SettingsPageViewModel` then selects System Default without updating the theme service or deleting its persisted override.

The Settings page therefore appears reset, but the previous light or dark override is loaded again on the next launch.

## Goals

1. Apply System Default immediately when Restore Defaults succeeds.
2. Remove the persisted Windows theme override so a new theme-service instance starts at System Default.
3. Keep the existing early-startup theme-loading path.
4. Notify active windows and overlays through the existing theme-change event.
5. Keep restore failure explicit and prevent the Settings page from displaying defaults when the use case fails.
6. Preserve telemetry behavior for an effective theme change.

## Non-goals

- Do not migrate all settings into Windows local settings.
- Do not delay theme initialization until the JSON settings repository is available.
- Do not change how the system light/dark preference is detected.
- Do not change the available theme choices or restart-message policy.

## Functional requirements

### Theme service reset

- Add an explicit reset operation to `IThemeService`.
- Remove `themeSetting` from Windows local settings even if the current in-memory theme is already System Default.
- Set the current theme to System Default.
- Raise `CurrentThemeChanged` when the effective selected theme changes.
- Track the resulting app-theme setting change using the existing telemetry event.

### Restore Defaults integration

- Inject `IThemeService` into `RestoreDefaultsUseCase`.
- Reset the theme alongside clearing ordinary settings, telemetry consent, and language override.
- Allow platform persistence failures to flow through `IUseCaseExecutor` as an explicit failed response.
- Refresh Settings page values only after a successful restore response.

### Persistence boundary

- Encapsulate Windows local-settings access behind an internal theme settings store.
- Keep production storage backed by `ApplicationData.LocalSettings` and the existing `themeSetting` key.
- Allow deterministic tests to reuse one fake store across theme-service instances to represent an application restart.

## Test plan

- Verify `RestoreDefaultsUseCase` requests a theme reset and retains all existing reset/save behavior.
- Verify resetting a stored non-default theme immediately selects System Default and raises the change event.
- Verify the persisted override is removed and a new theme-service instance initializes to System Default.
- Verify updating a theme still writes the selected override.
- Verify a failed restore response does not make the Settings page appear reset.
- Run all non-UI tests and build the WinUI x64 Debug project.

## Acceptance criteria

- [x] Restore Defaults immediately applies System Default through the theme service.
- [x] The Windows `themeSetting` override is removed.
- [x] A subsequent theme-service initialization remains at System Default.
- [x] Active theme consumers receive the existing change notification.
- [x] Failed restore operations do not update the Settings page to apparent defaults.
- [x] Existing non-UI tests pass and the WinUI x64 Debug project builds.

# Capture onboarding and standalone Text Extraction

The capture-analysis invitation now appears the first time the main window is shown without analysis consent. Its aurora and spark artwork introduces practical text, recording and naming features. Enable scanning grants analysis consent and enables scanning for new captures, then offers the existing separate choice to analyze history. Not now leaves scanning off and is remembered across restarts. The Details Text/Transcript tab can reopen this invitation, and its Enable capture analysis button remains available after dismissal.

The main window owns and serializes analysis, standalone OCR and telemetry consent dialogs. Hidden/closed startup windows cancel their invitation; a cancelled window lifetime is not recorded as a decision. Basic Details never opens a consent prompt. Restoring a remembered Text tab after capture or navigation also does not trigger a prompt automatically; selecting the tab or its enable button is an explicit entry. Dialogs follow the main window’s theme.

## Independent Text Extraction

The existing image-toolbar tool keeps the `ImageEdit_TextExtraction` feature flag, original Text Extraction consent dialog and original `Settings_AiConsent_TextExtraction` preference. It renders the current editor image, performs OCR and QR detection, and retains edit-revision/cancellation protection. It neither reads the analysis catalog nor grants scanning consent. The unused metadata-to-toolbar reader and its request payload have been removed. Other AI editing tools retain the capture-analysis consent owner; this change isolates Text Extraction specifically.

Capture-analysis consent cannot authorize the standalone tool, and revoking analysis cannot cancel its independent work. The standalone permission is revoked when its own settings value is reset or changed. Existing explicit standalone grants remain valid.

## Startup errors

Unrequested storage/policy failures while capture analysis is disabled remain in service state without producing an immediate background error toast. Explicit settings refresh/retry, save failures and errors during enabled analysis continue to report failures. This does not discard or repair existing protected data. The exact message from the reported production startup error was requested separately; a specific persisted-data fault must be diagnosed from that message rather than inferred from the lack of consent.

## Review

Use `artifacts/pane/app/CaptureTool.Presentation.Windows.WinUI.exe`. On a fresh profile, decline the welcome and use toolbar Text Extraction: it should ask its own permission and return OCR/QR content while scanning remains off. Restart: the welcome should stay dismissed. Open Details > Text: the analysis invitation should appear again. Enable it and accept the separate history scan to populate the pane. Check both themes and narrow windows.

Native screenshots and isolated lifecycle results are recorded under `tests/CaptureTool.UiTests/TestResults/artifacts/capture-onboarding`. UI fixtures verify integration and layout with synthetic providers. The Windows OCR/QR adapter also has a direct current-image check. Managed verification passed: 421 application tests, 325 infrastructure tests and 282 presentation tests. Nine Windows OCR adapter checks also passed, including a real locally generated image with text and a QR code on this PC. The final x64 native AOT build and all 22 desktop regression flows passed. The native suite covers first launch, decline/restart, independent OCR and QR, on-demand analysis, all six locales, existing pane/settings/naming flows, and light/dark/narrow visual artifacts. The four previously accepted Betalgo vendor trim/AOT warnings remain; no new production warnings were introduced. The additional English narrow overlay interaction check passed: declining, reopening and accepting consent keeps the pane usable, and independent OCR still works afterward.

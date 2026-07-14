# PRD: Image Edit Text Extraction Mode

## Summary
Add a feature-flagged Text Extraction mode to the image edit page. The mode asks for per-feature AI consent before first use, runs local OCR against the current edited image state, and displays a dimmed overlay with transparent cutouts over detected text areas.

## Goals
- Add a command bar toggle for Text Extraction mode on the image edit page.
- Hide Text Extraction behind `ImageEdit_TextExtraction`.
- Keep Super Image Resolution hidden behind `ImageEdit_SuperResolution`.
- Ask for AI consent before the first use of Text Extraction.
- Persist consent and expose it on the settings page.
- Add Super Image Resolution to the same AI consent settings section.
- Allow users to rescind consent for each AI feature independently.
- Run OCR when Text Extraction mode opens after consent is granted.
- Show OCR metadata as one cutout overlay path: text areas transparent, non-text areas dim gray with reduced opacity.
- Close OCR mode when another image edit mode is selected.
- Re-run OCR when the image edit history changes after the last OCR run and the user opens Text Extraction again.
- Keep the implementation aligned with DDD concepts by separating domain model, application contracts, infrastructure adapters, and presentation state.

## Non-Goals
- Exporting extracted text.
- Editing extracted OCR text.
- Persisting OCR results across app launches.
- Saving the dimmed overlay into exported images.
- Sending images or extracted text to a remote service.

## User Experience
1. The image edit command bar shows a Text Extraction toggle only when `ImageEdit_TextExtraction` is enabled.
2. When the user turns it on for the first time:
   - A dialog explains the AI feature and asks for consent.
   - If the user accepts, consent is saved and OCR runs.
   - If the user cancels or refuses, OCR does not run and the toggle returns to off.
3. After consent is granted, opening Text Extraction does not ask again.
4. If consent is later rescinded in Settings, the next attempt asks again.
5. While OCR runs, the page shows a loading indicator.
6. Once OCR completes, the canvas is dimmed except for transparent cutouts over recognized text areas.
7. Selecting any other image edit mode closes Text Extraction mode and removes the overlay.

## Domain Model
- `AiFeatureId`: stable identifier for a consent-controlled AI feature.
- `AiFeatureConsent`: value object describing a feature and its setting.
- `RecognizedTextDocument`: OCR aggregate containing full text, image size, and recognized regions.
- `RecognizedTextRegion`: text plus bounding geometry in image coordinates.
- `TextExtractionOverlay`: geometry projection from OCR regions to a visual cutout model.

## Application Contracts
- `IAiFeatureConsentService`: reads and writes persisted consent for a specific AI feature.
- `IAiFeatureConsentDialogService`: presentation adapter used by application-facing view models to ask for first-use consent.
- `ITextExtractionService`: infrastructure-backed OCR port.
- `ITextExtractionFeatureAvailability`: feature flag adapter.

## Infrastructure
- `WindowsTextExtractionService` uses Windows OCR capabilities and maps platform results to domain/application models.
- Feature availability adapters continue to use `IFeatureManager`.
- Settings remain persisted through `ISettingsService`.

## Presentation
- `ImageEditPageViewModel` owns Text Extraction mode state, consent flow, OCR cancellation, stale-result detection, and status messages.
- `ImageCanvas` owns only visual rendering of the overlay from projected text regions.
- OCR results are not added to `Drawables`; they are transient view state.

## Work Items
1. Add this PRD and maintain the work-item list.
2. Add feature flag `ImageEdit_TextExtraction`.
3. Add AI consent settings definitions for Text Extraction and Super Image Resolution.
4. Add AI consent application abstractions and settings-backed implementation.
5. Add settings page AI consent section with per-feature checkboxes.
6. Add Text Extraction domain/application contracts.
7. Add Windows OCR infrastructure service and dependency injection registration.
8. Add Text Extraction feature availability adapter and dependency injection registration.
9. Add WinUI consent dialog adapter.
10. Add Text Extraction mode state and command to `ImageEditPageViewModel`.
11. Add command bar toggle and loading state to `ImageEditPage`.
12. Add OCR overlay dependency properties and rendering to `ImageCanvas`.
13. Add stale OCR tracking based on edit-history revision.
14. Add unit tests for consent, settings, feature availability, view model OCR flow, and overlay geometry.
15. Run targeted tests and a solution build.

## Open Implementation Notes
- Prefer Windows App SDK AI `Microsoft.Windows.AI.Imaging.TextRecognizer` when the installed package exposes the necessary managed projection. If that API is unstable, fall back to `Windows.Media.Ocr.OcrEngine` while preserving the same application contract.
- Inflate OCR region bounds slightly for readability, then clamp to the displayed image bounds before rendering cutouts.
- Use a single even-odd XAML `Path` for the cutout overlay.

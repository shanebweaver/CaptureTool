# Capture Memory Home experience

Issue: #470

## Purpose

Capture Memory adds an explicitly enabled content-search experience to Home. When the feature flag is off, or the user has not opted in, the existing recent-capture gallery continues to behave as before. An empty search query always shows that gallery.

## DDD and dependency direction

The Home experience composes the existing Capture Analysis application boundaries instead of reading metadata documents or durable file paths directly.

- `CaptureMemoryHomeViewModel` owns presentation state: setup, preparation, index progress, cancellable search, result explanations, and recovery states.
- `ICaptureMemorySearchService` accepts bounded text and returns stable capture IDs plus bounded evidence. It never exposes a source path.
- `ICaptureMemoryResultResolver` belongs to the Library application boundary. It resolves a stable capture ID to a current, short-lived display/open location.
- `IOpenCaptureMemoryResultUseCase` resolves again at invocation time and delegates to the established recent-capture opener, so a cached UI path is not trusted.
- `ICaptureAssetRemovalService` performs Forget History. The presentation layer never deletes metadata or source files itself.
- Infrastructure combines `CaptureAnalysis_Platform` and `CaptureMemory_Search`; both flags must be enabled before any Memory UI is visible.

The feature therefore depends inward on application contracts. WinUI supplies controls, thumbnails, keyboard interaction, localization, and accessibility only.

## Opt-in sequence

1. Home shows a compact disclosure that names the local-only purpose and generated metadata classes.
2. The user chooses future captures, or future plus existing captures.
3. The exact `CaptureAnalysisPolicyDefaults` disclosure is submitted through the policy command service.
4. Required recipe capabilities are prepared with progress. Image descriptions are optional; an unsupported description model leaves filename and OCR search available.
5. Existing-capture authorization is a separate explicit command.
6. Home polls the durable policy while backfill is active and marks results as partial until it completes.

The UI does not duplicate authorization rules. Application policy remains the authority for admission and analysis.

## Search and result lifecycle

- Input is debounced and each query cancels its predecessor.
- A generation fence prevents a provider that completes late from replacing newer results.
- Empty or whitespace-only input clears the disposable result collection and restores the recent gallery without calling search.
- Result cards show thumbnail, captured date, media type, bounded evidence, and one of Text match, Visual match, or Filename match.
- OCR pixel geometry is rendered as an overlay when available.
- A result path exists only on the display model and is discarded when the model is removed or marked missing.
- Open resolves the capture again. A moved or missing source becomes a recoverable result state.
- Forget History calls the durable cleanup boundary and removes the result immediately after success.

## Recovery and accessibility

The presentation state distinguishes preparation, indexing, partial results, no matches, optional-description unsupported, source missing, failed search, and corrupt disposable projection. A corrupt projection can be rebuilt through `ICaptureAnalysisMaintenanceService`.

The search box has a localized accessible name and `Ctrl+F` focus accelerator. `Down` enters results, arrow keys use native list navigation, `Enter` opens, and `Escape` clears and returns focus. Result rows expose a combined accessible name containing filename, date, type, explanation, and bounded snippet.

## Privacy constraints

- Search text, snippets, OCR, descriptions, filenames, and paths are not sent to telemetry.
- Search contracts never expose source paths.
- Result locations are resolved only for preview/open and are not persisted by presentation.
- Thumbnails are Windows-generated in memory.
- Projection, metadata, and UI-test marker files are app-created non-user content and may be safely rebuilt or deleted by their owning app boundary.

## Verification

Presentation tests cover cancellation, stale-result suppression, explanations, empty-query gallery behavior, missing sources, corrupt projection, no matches, and optional description support. The desktop UI test exercises opt-in, a synthetic capture, indexing completion, search, explanation, open, and Forget History through automation IDs.

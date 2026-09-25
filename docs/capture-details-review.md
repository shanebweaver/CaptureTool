# Shared capture details

Branch: `codex/capture-details`, based on `codex/capture-analysis-core`.

## Review path

On Home, right-click a recent capture and choose **Details**. Keyboard users can focus a capture and press **Shift+F10** or the context-menu key. The same dialog supports images, audio, and video. Richer Home cards and search remain separate work.

The dialog presents:

- AI-suggested title, summary, category, and topics, explicitly labeled as suggestions.
- Extracted facts and QR content, with individual copy actions.
- Expandable source evidence for suggestions and facts. Evidence includes the original source entry and video/audio timestamps where available.
- Recognized text, transcripts, and AI descriptions, with copy-all actions and scrollable source lists.
- Media-specific file properties, identifying the analyzed file and when its properties were recorded. Known capture time is separate from filesystem dates; unknown values are not invented.

Opening or refreshing Details reads existing metadata; it never schedules analysis, downloads models, or requests another consent. Existing captures can be analyzed through Settings. Results update at analysis step boundaries while the dialog is open. Earlier successful results and limited processing coverage have explicit notices. URLs and QR values are plain, copyable text; opening links or executing actions is outside this change.

## Architecture and edge cases

`ICaptureDetailsReader` is the application boundary. It resolves an unambiguous asset identity, checks the current file bytes against the stored source revision, and reads normalized metadata and execution state. A generation change during the read discards the snapshot. Missing, changed, or inaccessible files do not expose unverified old results.

`CaptureDetailsContent` projects typed payloads into presentation rows. The view has no knowledge of provider responses, encrypted document formats, or inference APIs. Clipboard operations use the existing service. No metadata is written to logs or added to another cache.

Each dialog has a transient view model and cancellation lifetime. Reads and projection run away from the UI thread, concurrent refreshes coalesce, and closing the dialog unsubscribes observers and suppresses late completions. Deletion clears visible content and invalidates pending reads. Unknown media properties are omitted, except that an unknown capture time is explicitly shown.

## Demo preparation

Prepare models in the actual demo application's data directory and analyze a small set of real captures before presenting. The isolated test harness's model cache and synthetic UI fixtures are not a prepared production library. Use a screenshot with a useful fact or QR code, a short audio recording, and a short video to review the shared view. AI suggestions can be absent even when other useful metadata is available.

The UI automation fixture is enabled only with both `--capturetool-ui-test` and `--ui-test-details`; it never invokes real models. Synthetic values in its screenshots are layout and integration evidence, not a model-quality demonstration.

## Verification

- Application and presentation regression suites: **701 passed** (423 application, 278 presentation), including 18 new reader/presentation cases for all three media kinds, source evidence, timestamps, changed bytes, ambiguous paths, protected-storage errors, deletion races, clipboard failures, legacy results with unknown run identity, and closure during a read.
- Native x64 publish passed the repository's diagnostic guard with only the four previously accepted vendor warnings. Empty and populated collections use the app's established bindable collection shape; no new AOT suppression or dependency was added.
- **13 native desktop flows passed**: Details in all six languages, consent/settings in all six languages, and existing OCR. Includes keyboard entry, automatic progress-to-results updates, individual and full-source clipboard copying, source inspection, changed-file rejection, deletion, and Escape dismissal. Screenshots cover light, dark, compact, empty, changed-file, evidence, facts, source text, and file-property states. Logs/TRX files are in `artifacts/details-*` and `tests/CaptureTool.UiTests/TestResults`; screenshots are under `tests/CaptureTool.UiTests/TestResults/artifacts/capture-details/<locale>`.

Physical ARM64, offline isolation, and spoken Narrator checks remain the previously documented environment gates. This change adds no model or provider dependency.

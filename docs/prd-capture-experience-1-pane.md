# Capture experience — step 1: shared editor pane and local details

Status: Implemented; review and verification recorded in [capture-experience-review.md](capture-experience-review.md).

## Outcome

Someone viewing an image, audio recording, or video can identify the capture and inspect useful file properties without leaving the editor. The pane works with AI disabled and with files that have never been analyzed.

This replaces the Home Details dialog. Home remains a way to open a capture; it does not gain another metadata surface. The editor hosts one shared right-hand pane, inline when space permits and dismissible as an overlay at narrow widths. Opening the pane never schedules analysis or prepares models. Analysis completion does not open it automatically. Preserve the user's pane preference across editor navigation.

## Experience

- One accessible Details toolbar toggle in all three editors.
- A compact header identifies the capture and shows available dimensions/duration/size. Detailed properties use compact label/value rows, with uncommon technical properties collapsed.
- Details includes file name, known capture time, file dates, format, size and media-specific dimensions, aspect ratio or duration. Unknown capture time is not inferred from filesystem dates.
- Copy path and Show in folder act on the displayed file. Missing or inaccessible files produce an inline explanation. Clipboard/launcher failures are contained.
- A short, explicitly identified AI summary may augment Details when verified results exist. Categories, topic chips, extracted-fact inventories, provider diagnostics, and per-result evidence cards are excluded.
- File properties describe the saved source. Pending editor changes are clearly identified; source properties are not presented as the exported result's properties.
- Basic details remain available while analysis is running, disabled, unavailable, or deleted. Analysis state belongs to its content, not a blocking overlay over the pane.
- Step 2 adds a Text/Transcript tab. Step 3 adds capture-name editing and generated names to the header.

## Architecture

Extract the Windows file-property reader from the existing analyzer behind a reusable application abstraction. The existing analysis adapter and the pane use the same reader. Do not introduce another execution queue or acquire an AI consent for this read.

Keep local file properties separate from verified optional analysis. Reuse the existing metadata reader's capture identity, source revision, deletion-generation, and cancellation safeguards. Slow verification must not delay rendering ordinary properties. Each editor owns a pane lifetime; navigation cancels outstanding work and removes subscriptions. Late results cannot repaint a closed view or restore deleted metadata.

The pane is shared presentation code with small editor-specific callbacks for later navigation to text. It must not know provider APIs or storage-envelope formats. Do not import the prototype's Home search or its entire analysis view model.

## Acceptance and review gate

- Image, audio and video display current local properties with scanning disabled, no metadata, and consent absent.
- AI deletion clears optional content while retaining ordinary properties. Changed source bytes hide stale analysis.
- Opening/closing, switching captures, narrow/wide layouts, keyboard focus and theme work in the real WinUI app.
- Existing capture, edit, OCR and save workflows remain intact.
- Meaningful reader/lifetime tests and native compilation pass; visually inspect compact and wide layouts.
- Pause for self-review of ownership, responsiveness, accessibility and error handling, fix material findings, then proceed to step 2.

## Non-goals

Home search/cards, an exhaustive metadata inspector, filesystem renaming, timeline markers, transcript editing, cross-capture features and new AI providers.

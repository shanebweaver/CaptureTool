# Text in the capture pane

The image editor's **Extract text** command opens the capture pane's **Text** tab. It is a shortcut button; Details is the active mode while the pane is open. The old floating copy toolbar and canvas loading indicator are no longer shown.

Opening Details or switching to Text only reads available results. The shortcut requests extraction when text is missing. Existing matching results, including completed empty results, are reused. The pane holds search, passage navigation, copy, QR actions, progress, and failures. Image selection uses the same renderer for saved and current-image text.

## Which image is analyzed

- An unchanged image uses the existing capture-analysis action and durable metadata cache. A separate working copy must match the saved source revision before its positions can be used.
- Once the image is edited, the pane hides source-file passages. Extraction renders the current editor image, including crop, orientation, and annotations, and keeps its results in `CaptureEditorTextSession`.
- Current-image results survive closing and reopening the pane during that editing session. They never replace the saved capture's analysis. An image edit invalidates them and cancels any pending extraction. Unknown text bounds remain copyable without a fabricated location.
- Closing the pane, leaving the Text tab, or choosing another edit mode cancels current-image extraction. Cancellation and edit-revision checks reject late provider results. Previously completed session results remain reusable.

Switching to Details or Summary clears the text overlay and keeps the pane open. X closes the pane; tools such as Crop switch away from Details. The command bar and footer remain outside the pane's content area.

Validation covers saved-result reuse, edited-image projection, empty-result caching, source mismatch, cancellation, and desktop navigation across saved and rotated images.

## Text layout

Saved OCR and current-image extraction preserve provider line and word indices. The shared `RecognizedTextGrouping` domain algorithm builds lines, finds nearby text in the same column, and separates paragraphs using local median text height and line spacing. It treats headings, paragraph indentation, and list markers as boundaries, while keeping wrapped list content together. Words retain their original text, bounds, timestamps, and evidence indices.

Columns are read down before moving to the next column, using provider order to choose the column order. Horizontal tolerances use character widths and vertical tolerances use text heights, so changing image aspect ratio does not change grouping. Unknown bounds, embedded line breaks, and different video timestamps stop geometric grouping. Ambiguous two-line gaps use a conservative threshold; three or more lines can establish a looser local spacing pattern.

The Text tab passes its calculated line and paragraph membership to the canvas. The renderer uses those boundaries directly for selection and copy. Fresh extraction uses the same domain algorithm. Copying multiple image-text passages includes a blank line between paragraphs. There is no LLM call or migration step.

Focused tests cover column ordering, paragraph gaps, indented first lines, wrapped lists, mixed word sizes, right-to-left order, independent axis scaling, dense text, persistence of OCR indices, and matching saved/editor passages. These geometric rules do not infer semantic table cells or reconstruct document formatting.

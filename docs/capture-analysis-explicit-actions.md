# Explicit capture analysis actions

This decision supersedes automatic post-capture scanning and analysis on opening a
capture, including the session-only queue policy's automatic entry points.

Taking a capture, launching the app, opening a file or pane, switching tabs, and
changing consent must not enqueue AI work. Basic local file details remain
available. Saved metadata remains readable without running a model.

Semantic actions prefer the [Windows language-model provider](windows-language-model-provider.md),
with the existing Phi-4 CPU model as a backup when Windows cannot serve the request.
They check candidate readiness before scheduling media prerequisites. This fallback
is part of the explicit action; it does not authorize background or startup work.
Phi-4 retains its high peak memory cost. After requested work drains it stays loaded
for up to 30 seconds for another explicit action, then unloads. Low memory, revoked
consent, cancellation, metadata deletion, and shutdown release it sooner once inference stops.

The Text tab offers one **Find text and QR codes** action for images and videos,
plus audio transcription where relevant. The combined scan queues OCR and QR
detection in one request and reuses either result when it is already available.
Before data is available, only the action is shown. The button keeps its label,
size, and position and is disabled while an animated Aurora edge follows its
border. Once scanning completes, results replace the button and its busy effect.
Opening saved text or QR data goes straight to results. A successful empty scan
shows a localized empty state in place of the search, navigation, and copy controls,
without an empty-text or empty-QR snackbar. Cached empty scans use the same state.
A failed scan with no data leaves the action available to retry. Search controls
appear when there is data, and an empty search never brings the scan button back.
The source selector lists only sources with results in the current capture and
is hidden when fewer than two sources are available. Search queries do not change
the available source choices. A hidden or unavailable source selection resets to All sources.
A Summary tab replaces the image-description section
with screenshot summary and alt-text suggestions, each with a copy action.
Feature descriptions appear in tooltips on info buttons beside the headings.
The info buttons support keyboard focus and pointer hover, with localized
accessible names and help text.
Their buttons keep a fixed label and size and are disabled during generation.
The same Aurora edge indicates generation without replacing the icon or label.
Once a result is available, the action row disappears and the generated text and
copy action take its place. Failed requests show an error snackbar, stop progress, and re-enable
the button for another attempt.
Name suggestions and transcription use this treatment too. The reusable XAML
control draws a flowing cyan, violet, and pink border with a soft halo through
Windows Composition. It keeps the native button and its layout intact, exposes
the busy state to assistive technology, and releases animations on unload.
Reduced motion uses a static gradient; high contrast uses a static system-color
border. No progress spinners are needed for these actions.
Successful results do not show a snackbar for bounded input selection. Coverage
remains in the saved metadata and model input; generation failures and source/read
problems still produce notifications.
Clicking either screenshot action authorizes OCR, visual description, and its one
selected LLM output. A name suggestion uses the same sources and its own prompt.
Already saved results (including empty OCR) are reused after verifying the source
revision. Audio/video summaries continue to consume available text metadata.
Each action requests selected capabilities from the common configuration. The worker preserves
configured order and fallbacks, runs one provider at a time, and persists each result
without replacing unrelated metadata for the same source revision.

Consent is requested at the point of action. Settings retain consent and deletion;
automatic scanning/naming toggles and first-launch AI consent are removed. The
same consent dialog and protected policy also cover standalone Text Extraction and
the other local AI editing tools. The old per-feature dialog and Settings checkbox
are removed. An old OCR-only setting does not grant broad AI consent; an existing
shared consent does. Standalone OCR execution remains independent of the pipeline.

Duplicate actions during a running capture are disabled. Results and empty results
are reused; failed actions may be explicitly retried. A cancelled request cannot
resume at launch, and closing the app still cancels/discards pending requests.

Verify passive events perform no inference, requested steps exclude unrelated
models, sequential actions preserve results, consent decline does nothing, and
deletion/revocation/shutdown reject late results. Run desktop checks in English only.

Reused steps persist the original result identity without changing its generation
time, provider, or producing run. The protected store atomically checks that identity
against the current source and request. Adding/replacing an input invalidates derived
results that consulted its old or absent identity. Summary and alt text have separate
payloads and are retained independently. Alt text is bounded to 400 characters and
requires resolvable evidence; an explicit abstention is a successful empty result.
Prompts treat source text as data, and the UI labels outputs as suggestions. No copy
action publishes content outside the local clipboard.

The local LLM uses bounded generation schemas and at most one correction within
the requested action; see [repeatability hardening](metadata-enrichment-stability.md).

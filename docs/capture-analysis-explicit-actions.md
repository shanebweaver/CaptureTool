# Explicit capture analysis actions

This decision supersedes automatic post-capture scanning and analysis on opening a
capture, including the session-only queue policy's automatic entry points.

Taking a capture, launching the app, opening a file or pane, switching tabs, and
changing consent must not enqueue AI work. Basic local file details remain
available. Saved metadata remains readable without running a model.

The details pane offers separate actions for visual descriptions, recognized text,
QR codes, and audio transcription where relevant. Summary/name generation consumes
existing text or descriptions; it never silently runs upstream models. Each action
requests selected capabilities from the common configuration. The worker preserves
configured order and fallbacks, runs one provider at a time, and persists each result
without replacing unrelated metadata for the same source revision.

Consent is requested at the point of action. Settings retain consent and deletion;
automatic scanning/naming toggles and first-launch AI consent are removed. The
standalone Text Extraction feature keeps its separate, explicit consent workflow.

Duplicate actions during a running capture are disabled. Results and empty results
are reused; failed actions may be explicitly retried. A cancelled request cannot
resume at launch, and closing the app still cancels/discards pending requests.

Verify passive events perform no inference, requested steps exclude unrelated
models, sequential actions preserve results, consent decline does nothing, and
deletion/revocation/shutdown reject late results. Run desktop checks in English only.

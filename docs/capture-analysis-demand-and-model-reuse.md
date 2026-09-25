# Requested captures and reusable models

The entry points below are superseded by [explicit analysis actions](capture-analysis-explicit-actions.md).
Capture creation and opening no longer run models; only action buttons request work.
The session-only queue and model lifetime decisions remain in effect.

This decision supersedes the earlier bulk-history scan prompt and Settings action.

New captures continue to use the consent and scanning policy recorded at capture
completion. Enabling scanning does not import or scan the capture library. Opening
an image, audio recording, or video queues that source only, after successful
navigation and only while scanning and consent are enabled. The Details invitation
also requests its current capture after consent is granted. Basic file details and
standalone Text Extraction retain their independent behavior.

Opening a saved alias resolves to the existing capture identity. An existing
attempt, whether pending in this session or finished, is not replaced on each open. This avoids
repeated inference on unsupported or malformed output. Deleting analysis removes
those attempts too; subsequently opening a capture can request fresh analysis.
Historical opens do not enroll captures in automatic naming.

Scan requests are session-only. Closing cancels active work and forgets the waiting
queue; a crash also cannot restore it. Startup does not start the worker, reconcile
capture registrations, or hash old sources to recover name suggestions. Persisted
run markers retain provenance, but unfinished runs without a current-session
request are reported as cancelled. Their tokens cannot publish late results.
Already committed metadata and accepted names remain available. Explicitly opening
a cancelled capture requests a fresh run; simply launching the app or visiting
Settings does not. The first new capture or explicit open starts the worker lazily.

The worker takes up to `MaximumBatchCaptures` requested captures (default 16) from
the current session's queue. It groups compatible next steps, retaining configured order and
fallback preference within each capture. Source revisions, authorization, run and
generation tokens are checked at every publication. A rejected commit that leaves
the same run without progress terminates that run instead of repeating inference.
Queue discovery reads only current requests rather than walking every historical
metadata record. Forgetting requests requires no disk writes or library scan.

Foundry owns one resident model, keyed by its resolved identity, behind an exclusive
lease. Compatible work reuses it, including synopsis and classification adapters.
Switching unloads the previous model first; idle and shutdown release resources.
Cancellation cannot unload a model while native inference still owns it. Cleanup
waits for the actual invocation to finish, and an unload failure prevents loading
another model over the retained allocation. The worker's resource lifetime contract
is optional for providers that do not retain expensive resources. Failed release is
reported as a provider failure, leaves storage available, and does not terminate the
worker or initiate an unbounded cleanup retry.

The reported memory cycles were consistent with per-step loading: inspection of
the installed application's run statuses showed the queue advancing, with four
fully successful captures, one completed capture with rejected classification,
and one failed run with rejected synopsis. It was not an endless retry of one
capture in that saved state. Missing-file probes can produce handled debugger
exceptions; JSON validation rejects invalid model output without publishing it.
Diagnostics now identify failed provider/step/outcome codes without logging source
paths, captured text, or generated responses.

Reusing a large model reduces repeated loading; it does not reduce that model's
resident memory requirement. Memory remains allocated while compatible work runs
and is released when it becomes idle or the provider changes.

## Verification

Session-only follow-up (2026-09-25): all 350 infrastructure tests passed. Coverage
includes active/queued shutdown, refusal of late results even when storage writes
are unavailable, idle startup/Settings with zero historical metadata reads, and
explicit reopening of one cancelled capture. The synthetic process-termination
harness passed all ten checkpoints, including model preparation/execution,
metadata publication, and deletion. It uses the production DPAPI store and fake
analyzers; these checks do not load AI models. Logs are
`artifacts/analysis-session-tests.log` and `artifacts/analysis-session-recovery.log`.
The updated x64 native app publish also passed; its output is in
`artifacts/pane/app` and the build log is `artifacts/analysis-session-publish.log`.

The infrastructure, Windows provider, application, and presentation suites passed
(338, 50, 424, and 289 tests). A subsequent focused worker run passed all 48 tests,
including an additional test for failed resource release. Coverage includes bounded
batches, rejected commits, model reuse and switching, failed loads/unloads, revoked
consent, and cancellation while native inference retains ownership.

The x64 native AOT publish passed with the four previously accepted Betalgo SDK
warnings. The installed CPU Phi model produced a synopsis and classification for
a synthetic invoice using the shared runtime. This verifies inference and reuse;
it is not a peak-memory benchmark.

English desktop UI checks passed for onboarding and independent OCR consent, the
Details pane analysis lifecycle, and Settings consent/progress/deletion. Other
locales were skipped. Test synchronization now waits for visible progress and an
enabled delete command, and invokes Home through its accessibility action rather
than clicking during a flyout transition.

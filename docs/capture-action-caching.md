# On-demand AI suggestions and saved metadata

Name suggestion, summary, and alt text have separate prompts, results, loading states, and failure states. Each action requests only its selected output and any missing source metadata:

| Action | Saved payload | Generated text |
| --- | --- | --- |
| Suggest name | `CaptureNameMetadata` (`capture-name`) | One name, up to 160 characters |
| Summarize | `CaptureSynopsisMetadata` (`capture-synopsis`) | One summary paragraph |
| Generate alt text | `ImageAltTextMetadata` (`image-alt-text`) | One accessible image description |
| Classification | `CaptureClassificationMetadata` (`capture-classification`) | Category and tags |

Summary generation no longer asks the model for a name. The summary prompt contract is version 4; the name contract starts at version 1. Older combined synopsis results remain readable, and an existing title remains usable as a name suggestion without new inference.

For images, OCR and image description run first when missing, followed by the requested LLM output. Audio and video use available metadata and retain the requirement to extract source text first. Other LLM outputs, OCR, transcription, and QR scans run only when explicitly requested. Saved results are shown without another model call.

After a requested Phi action finishes, the worker reports idle and keeps the model loaded for up to 30 seconds. Another explicit request wakes it immediately and resets the window when work finishes. This avoids loading Phi again for nearby actions without generating unused outputs. It saves model loading time; each requested output still takes inference time. Prompts use fresh request context and validated evidence.

Vision and speech models unload immediately when idle. Low physical memory reported by Windows, revoked consent, cancellation, metadata deletion, and shutdown bypass Phi's idle window. Active native inference retains model ownership until it returns; cleanup cannot unload underneath it. Only model weights remain warm, not a conversation context.

The name button opens its suggestion for review. Generation never renames a file automatically. Cached outputs do not show another loading indicator while a different output is pending.

## Failure handling

Classification prompt version 4 uses a flat category, one source reference, and a short list of topic strings. The category and every topic must be supported by that source. The parser preserves the reference on each saved topic and rejects unknown categories, missing or fabricated references, and malformed or oversized topics. It validates every topic before removing duplicates and retaining at most five; selecting fewer valid suggestions does not require another model call. The Foundry schema keeps nullable values outside string enums to match the SDK transport.

If a metadata call exceeds its deadline and native inference has not stopped, the worker records that step's failure and retains the remaining steps. It waits for the native call to finish before continuing; late results cannot publish. Cancellation and consent revocation still cancel the requested work.

`Windows language model readiness: NotSupportedOnCurrentSystem` is an availability report. The worker proceeds to the configured Foundry fallback.

## Cache behavior

- Every successful step is committed to the protected metadata store before the next step starts. A later failure does not discard successful OCR, description, or other independent results.
- Actions request reuse. Before preparing or invoking a provider, the worker checks for a saved result of that capability belonging to the same source revision. Empty results and explicit abstentions also count as completed results.
- Saved results survive reopening the capture and restarting the application. Pending work is forgotten on shutdown; completed results remain available.
- The source is fingerprinted and verified again before publication. Changed source bytes cannot reuse old results. Replacing a consulted source result invalidates derived results that depend on its identity.
- Name and summary consume OCR, descriptions, and transcripts. Alt text consumes OCR and descriptions. They do not depend on QR scans, so detecting a QR code later does not invalidate newly generated text. Legacy results retain their original evidence dependencies.
- Explicit metadata deletion still removes all analysis. It does not remove capture files or an accepted filename.

Regression coverage checks separate loading/failure states, both action orders, protected round trips, fresh workers and reopened stores, empty-result reuse, retry reuse, unrelated QR additions, and changed-source invalidation. UI checks run in English only.

## On-demand and idle-reuse validation

465 focused application, infrastructure, Windows-provider, and presentation tests passed.
The x64 app and diagnostic harness built with zero warnings or errors. Both English
desktop scenarios passed: independent alt text/summary/name requests with shared
consent, and name review/acceptance/cache/restart. The rename scenario required a
rerun after a clipboard-notification timeout; no product change was needed for it.

Worker tests cover the 30-second idle deadline and reset, cached source reuse,
an idle UI during retention, and early release on low memory, consent revocation,
cancellation, deletion, and shutdown. Active native ownership is preserved.

The native cached-Phi check measured 8.801 seconds for cold acquisition, less than
0.001 seconds for warm acquisition after a two-second pause, and 8.818 seconds after
explicit release. Windows reported no low-memory pressure. This checks real model
loading, reuse, release, and the memory-signal API; deadline behavior is covered by
worker tests. It runs no inference, so these timings do not measure generation.
Logs are `artifacts/on-demand-native-reuse.log` and `artifacts/on-demand-*.log`.

## Earlier prompt validation

The earlier eager batch implementation was validated on the development PC with 366 focused tests and two English UI scenarios. The current on-demand behavior supersedes its automatic companion generation. Desktop scenarios use test providers.

The native follow-up reproduced `invalid-text-schema` on both generated screenshots. Classification tried twice, taking 59.6 seconds on the dense screenshot and 42.0 seconds on the invoice before alt text could start. Both alt-text requests eventually succeeded, so the reported missing-output case was not reproduced directly. The updated full batch produced name, summary, and alt text on both fixtures, with classification last. A further classification-only run passed on both fixtures on the first attempt after adding bounded topic selection. These are local CPU fixture timings, not a general performance benchmark. Logs are `artifacts/llm-batch-repro.log`, `artifacts/llm-batch-fixed-native.log`, and `artifacts/llm-classification-native.log`.

The subsequent classification fix passed 401 focused analysis tests and two English desktop scenarios. The app build completed with zero warnings or errors. Regression coverage includes invalid evidence and topics, duplicate/excess topic selection, and continuing explicitly requested work after a native metadata call exceeds its deadline without accepting its late result.

Earlier prompt validation passed all ten stages across a dense development screenshot and an invoice. All six native name/summary/alt-text generations passed on their first attempt. Asking for shorter text removed length-overflow correction attempts observed in the initial name and summary prompts. This is a fixture check, not a guarantee for every capture.

# Independent capture actions and saved metadata

Name suggestion, summary, and alt text are separate explicit actions. Each has its own capability, result, loading state, and failure state:

| Action | Saved payload | Generated text |
| --- | --- | --- |
| Suggest name | `CaptureNameMetadata` (`capture-name`) | One name, up to 160 characters |
| Summarize | `CaptureSynopsisMetadata` (`capture-synopsis`) | One summary paragraph |
| Generate alt text | `ImageAltTextMetadata` (`image-alt-text`) | One accessible image description |

Summary generation no longer asks the model for a name. The summary prompt contract is version 4; the name contract starts at version 1. Older combined synopsis results remain readable, and an existing title remains usable as a name suggestion without new inference.

For images, each action explicitly requests OCR and image description before its own generation step. It does not request either of the other text-generation actions. Audio and video name/summary actions use available metadata and retain the existing requirement to extract source text first. Work remains serialized to bound resource usage; an unrelated action can be temporarily disabled without showing a loading indicator.

## Cache behavior

- Every successful step is committed to the protected metadata store before the next step starts. A later failure does not discard successful OCR, description, or other independent results.
- Actions request reuse. Before preparing or invoking a provider, the worker checks for a saved result of that capability belonging to the same source revision. Empty results and explicit abstentions also count as completed results.
- Saved results survive reopening the capture and restarting the application. Pending work is forgotten on shutdown; completed results remain available.
- The source is fingerprinted and verified again before publication. Changed source bytes cannot reuse old results. Replacing a consulted source result invalidates derived results that depend on its identity.
- Name and summary consume OCR, descriptions, and transcripts. Alt text consumes OCR and descriptions. They do not depend on QR scans, so detecting a QR code later does not invalidate newly generated text. Legacy results retain their original evidence dependencies.
- Explicit metadata deletion still removes all analysis. It does not remove capture files or an accepted filename.

Regression coverage checks separate loading/failure states, both action orders, protected round trips, fresh workers and reopened stores, empty-result reuse, retry reuse, unrelated QR additions, and changed-source invalidation. UI checks run in English only.

Validation on the development PC: 231 focused tests and two English UI scenarios passed; the x64 app build completed without warnings. The final native run passed all ten stages across a dense development screenshot and an invoice. All six name/summary/alt-text generations passed on their first attempt. Asking for shorter text removed length-overflow correction attempts observed in the initial name and summary prompts. This is a fixture check, not a guarantee for every capture.

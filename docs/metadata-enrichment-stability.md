# Repeated summary and alt-text generation

## Current screenshot contract (adapter version 3)

Testing actual screenshot prerequisites uncovered failures missed by the original
prepared-metadata corpus below. A 1920x1080 synthetic development screenshot failed
image description: the local Responses runtime reported 2,099 image/input tokens
against a 572-token sequence budget. A synthetic invoice produced eight citations
for alt text on both attempts, despite the four-citation limit. The dense image
also produced malformed alt-text JSON and a summary copied from the prompt's
unrelated library example.

The current implementation:

- Encodes a separate vision input with its longest edge at most 1,024 pixels,
  preserving aspect ratio and never upscaling. OCR keeps its existing decode size.
  The Responses request reserves 1,536 tokens to cover up to 1,024 visual tokens
  plus the original 512-token generation budget; the observed runtime calculates
  its sequence limit before adding image tokens. This avoids the input-overflow
  error while preserving more detail than a tiny thumbnail.
- Asks vision for layout and subjects; OCR supplies exact text. The text prompt
  explicitly prefers OCR for wording, numbers, paths and commands over inferred
  descriptions. This improves grounding but cannot guarantee factual accuracy.
- Removes the unrelated user/assistant example exchange from Foundry requests.
  Each request contains instructions and the current capture's sources only.
- Uses one primary numeric source ID, independently checked against supplied
  entries. Synopsis uses `{"title":"title","summary":"short paragraph","evidence":0}`;
  the summary is one paragraph of at most 400 characters. Both suggested values
  share the primary citation; the derivation still records all consulted inputs.
  Alt text uses a flat transport object:
  `{"altText":"suggestion","evidence":0}`, or both values null to abstain.
  The stored metadata/evidence types are unchanged; existing records remain readable.
- Accepts a single enclosing JSON Markdown fence as formatting, then validates
  the enclosed JSON normally. Extra prose, malformed JSON and invalid evidence
  still fail; no speculative JSON repair is performed.
- Retains one bounded correction, strict parsing, source identity checks, and
  cancellation ownership. Invalid output is never published just to hide a failure.
- Classifies unsupported Windows image capabilities as unsupported rather than
  transient failures. Worker wakeups coalesce without a caught semaphore overflow.
  Vision failures now distinguish server errors, incomplete output, refusals,
  identity mismatches and HTTP status without logging capture content.

The opt-in `CaptureTool.Analysis.Smoke <absolute-output-directory> --screenshot-checks`
command generates an invoice and a dense
development screenshot, then runs native OCR, image description, summary/title,
and alt text. It requires cached models and never reads user captures or enables
application scanning. Raw synthetic responses are saved only by this diagnostic.
It also checks expected fixture subjects, so valid JSON about the unrelated example
does not count as a pass.

Verification on this PC, 2026-09-25: two fresh-process runs completed all 16 stages
(OCR, vision, synopsis/title and alt text for both images). All eight text-generation
actions succeeded on their first attempt, including switching from summary to alt
text on a resident model and unloading/reloading between captures. Text actions
took 16.6–45.3 seconds; descriptions took 11.0–12.8 seconds. The earlier screenshot
baseline failed three stages and also returned an unrelated example-based summary.
All 169 focused provider, protocol, worker, consent/admission and configuration
tests passed. These are synthetic fixture checks, not a factual-accuracy guarantee
or qualification of every screenshot or Windows/NPU device.
The complete x64 Native AOT application publish also passed with only the existing
documented Foundry SDK converter warnings.

## Earlier version 2 qualification

The `invalid-text-output` report was reproduced with the cached
`Phi-4-mini-instruct-generic-cpu:5` model on this x64 PC. The initial run made twelve
calls across three synthetic images: an invoice, a dense development workspace,
and a landscape with no text. Four calls failed: both outputs for the dense image,
both after unloading/reloading and while retaining the model. All four responses
were complete JSON but cited seventeen sources per suggestion. The domain permits
at most four. The prompt described this limit; the generation schema did not.

The adapter now supplies the text, collection, and citation bounds in its JSON
schema. Citation IDs use numeric ranges: the installed Foundry Local 1.2.4 schema
transport rejects numeric `enum` values before inference, because it expects string
enums. Each capability has a distinct schema name. Independent parser and domain
validation still reject fabricated references, duplicate fields, oversized text,
wrong model identities, tool calls, and truncated output.
The installed runtime did not reliably honor the array bounds in real inference;
the correction and independent validation therefore remain necessary.

A mechanically invalid response gets at most one fresh correction request under
the same model lease and existing two-minute execution deadline. The correction
asks for shorter text and one relevant source per suggestion. It receives the same
bounded source metadata, without repeating OCR or image description, accepting an
invalid first response, or feeding that response back as instructions. Refusals,
HTTP errors, identity mismatches, and cancellation do not trigger correction.
Native ownership continues until the actual call ends; revocation fences results
and prevents further inference.

Failure codes now identify the rejected constraint, such as
`invalid-text-evidence`, `invalid-text-text-bounds`, `invalid-text-json`, or
`invalid-text-truncated`. Production diagnostics never include captured text or raw
responses. Successful results record semantic adapter version 2. Existing valid
metadata is preserved.

The opt-in `CaptureTool.Analysis.Smoke --stability-checks` harness exercises the
production protocol and correction loop with hard-coded synthetic metadata only.
It requires an already cached Phi model and never downloads models. It alternates
summary and alt text, first unloading after each action, then reusing the resident
model. Only this synthetic diagnostic saves raw responses, under a timestamped
`stability` directory. These checks measure response validity and source-reference
integrity; they do not guarantee the factual accuracy of every generated sentence.

Verification on this x64 PC (2026-09-25): all eighteen summary/alt-text actions
completed successfully across three rounds of the same fixtures. Twelve succeeded
on the first attempt; all six dense-image actions succeeded after one correction.
Elapsed time per action was 13.4–95.7 seconds, including model acquisition and
listener startup, within the existing two-minute budget. The first round unloaded
after every action; the remaining rounds reused the resident model. The measured
baseline was four failures in twelve actions. This is a repeatability check for
these fixtures and this installed CPU runtime, not a guarantee for every input or
hardware configuration.

Twenty-nine targeted protocol/model-lifetime tests passed, including bounded
correction, cancellation, malformed output, and invalid references. A workflow
regression test also verifies that a failed alt-text action can be retried while
reusing its successful OCR and description results.
The x64 native release publish passed with the existing documented SDK AOT warnings.

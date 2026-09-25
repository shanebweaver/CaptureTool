# Metadata enrichment — slice 3: titles, summaries, and classification

Status: planned; depends on review of slices 1 and 2.

## Goal

Generate small, grounded suggestions that help identify a capture, using existing metadata and a stable on-device text provider.

## Outputs

- Suggested title: one concise title, separate from file names and user-authored titles. No automatic rename or overwrite.
- Summary: a bounded plain-text description of the capture's content. Keep evidence for factual statements and disclose limited source coverage in the metadata contract.
- Classification: at most one primary category from a small versioned vocabulary, plus a small bounded set of topics. Initial vocabulary proposal: document, conversation, code, error, web-content, media, other. Confirm with representative captures before freezing v1. Allow abstention rather than forcing a category.
- Keep typed, versioned payloads and separate capabilities so partial success is useful. Each output declares all consulted first-level results, including missing optional inputs, and retains evidence.

## Provider and architecture constraints

- Evaluate the stable local text APIs available at implementation time against supported devices, release-channel requirements, languages, licensing/distribution, offline operation after preparation, cancellation, context limits, and native AOT. Existing image-description and speech providers do not automatically establish text-generation support.
- Document the selected provider and any stable fallback before adding a dependency. Do not assume a preview, limited-access offering, or announced replacement is generally available. If no qualifying provider can be shipped, retain tested contracts and fixtures, leave the capability unconfigured, and identify the external blocker honestly.
- Keep model-specific prompting, preparation, output parsing, and refusal handling inside adapters. Central code configuration defines processor order, candidates, and limits. An unsupported device skips semantic insights gracefully while first-level analysis and deterministic facts remain useful.
- Treat OCR, transcripts, QR payloads, and descriptions as untrusted input text, never as instructions. No tools, network actions, or captured-text logging in the generation path.
- Use bounded context assembly with source identifiers and explicit coverage. Do not silently summarize a truncated transcript as if it covered the entire recording.
- Validate model outputs against schemas, allowed categories, length limits, and resolvable evidence. Reject fabricated evidence or malformed output; allow bounded retry/fallback using the shared execution policy.
- Evidence grounded in a model-generated description remains inferred. Do not promote it to an observed fact. Avoid numeric confidence scores unless the provider actually supplies a meaningful calibrated measure.

## Acceptance

- Small representative fixtures for each media type, sparse/noisy OCR, empty speech, multilingual content, conflicting sources, prompt injection, insufficient context, and overlong input.
- Titles/summaries are supported by supplied content; no fabricated owners, dates, decisions, or obligations. Categories use the declared vocabulary; topics remain bounded and normalized.
- Tests cover adapter output validation and mocked unavailable/failing providers. Real-device results are recorded separately from mocks.
- No new presentation surfaces. Consumer contracts support later suggestions without overwriting user edits.

## Review gate

Review provider availability and measured quality before enabling semantic adapters by default. Lack of a suitable provider does not block shipping independently useful structured facts.

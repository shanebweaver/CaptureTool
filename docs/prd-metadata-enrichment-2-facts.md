# Metadata enrichment — slice 2: structured facts

Status: implemented on `codex/capture-metadata-enrichment-facts`, awaiting slice review. Built on the completed foundation branch after approval to proceed. See the [facts review](metadata-enrichment-facts-review.md).

## Goal

Extract useful literal facts from existing metadata with predictable, local rules. No model download or media access is needed.

## Scope and contract

- Introduce a metadata processor port with a read-only, bounded snapshot of its declared inputs. It receives no media path, persistence service, or UI service.
- Input selection records both present and absent declared capabilities. The initial extractor consumes OCR, decoded QR values, and transcript segments. Generated descriptions are excluded from observed facts.
- Implement one deterministic structured-facts adapter and its versioned descriptor. Keep extraction separate from scheduling. Integration into the durable worker is slice 4.
- Support explicit HTTP/HTTPS URLs, email addresses, validated ISO calendar dates, explicit currency-code amounts, and a documented narrow set of labeled error/reference code forms. Do not guess locales, currencies, time zones, missing years, account ownership, or product identities.
- Preserve the original source spelling in each fact. Any normalized lookup value is optional, typed, and must not replace the source value. Do not convert currencies or infer that an arbitrary number is an amount.
- Deduplicate matching kind/value pairs within a capture while retaining bounded distinct evidence locations. Do not combine text across unrelated OCR regions or transcript segments to invent a match.
- Evidence is checked against the exact source span. Rules must preserve UTF-16 offsets, including non-ASCII and surrogate-pair cases. QR payloads are data: never navigate, execute, or resolve them.
- Distinguish successful extraction with zero facts from missing inputs, unsupported data, cancellation, and failure. Mixed available inputs may produce partial useful results; the dependency snapshot explains what was considered.
- Bound input characters, output facts, evidence per fact, and execution time. Define deterministic truncation and retain coverage information so consumers cannot mistake a partial scan for a complete one.

## Acceptance

- A fixture corpus covers positive examples and common false positives: punctuation near URLs, invalid dates, plain numbers, ambiguous currency symbols, ordinary prose mistaken for codes, repeated video-frame OCR, multilingual text, empty input, and very large input.
- Facts are deterministic for the same input, configured limits, and adapter version. Results identify their processor and exact inputs.
- Cancellation and malformed input do not publish partial success. A deliberate input limit produces an explicitly partial result, not silent data loss.
- Run the relevant unit and storage regressions. No settings/editor/Home UX changes or automatic background scheduling yet.

## Review gate

Review extraction precision, bounds, normalization rules, and coverage semantics before adopting the adapter in production. Expand rule coverage only with representative fixtures.

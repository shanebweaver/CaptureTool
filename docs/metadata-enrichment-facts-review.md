# Metadata enrichment structured facts review

Date: 2026-09-24. Branch: `codex/capture-metadata-enrichment-facts`, based on the completed foundation at `2ad57deb`. Neither enrichment implementation branch has been merged into the parent feature branch by this slice.

## Delivered

- A metadata processor port and immutable, bounded text snapshot. The processor receives capture identity/revision, declared present/absent inputs, selected text entries, and coverage; it has no media path, store, UI, or model service.
- A deterministic `local-structured-facts` adapter, rules version `1`, with `local-rules` provenance. It consumes OCR, QR values, and transcript segments in that order. Descriptions do not become observed facts.
- Central code configuration for input order and budgets. Fact-specific output options remain separate from the generic metadata input contract. There is no new DI registration, background queue, settings control, or default worker step.
- Exact kind/value deduplication with distinct source locations. Matching is ordinal and preserves spelling/case, including different spellings of otherwise equivalent URLs or email addresses. No normalized lookup value is generated in v1.
- Protected persistence of coverage alongside facts. Missing coverage on older results means unknown, never complete. Nullable new DTO fields are omitted when absent, preserving the earlier legacy result identity representation.

## Rules version 1

| Fact | Accepted forms | Deliberate exclusions |
| --- | --- | --- |
| URL | Explicit HTTP/HTTPS URI, including local hosts and IP literals; preserve original text | Other schemes, bare domains, credentials in URI authority, malformed URIs |
| Email | ASCII local part and DNS labels, dotted alphabetic final label; plus tags supported | Internationalized/quoted mailbox syntax, single-label domains, invalid dots/labels |
| Date | Valid calendar date in `yyyy-MM-dd` form | Relative dates, missing years, slash dates, invalid leap days |
| Amount | Supported uppercase currency code before or after an ungrouped decimal, separated by spaces/tabs; optional minus, up to 15 integer digits and 2 fractional digits | Currency symbols alone, guessed codes, commas/grouping, leading-zero integers, exponent notation, locale inference |
| Error code | English `Error:` or `Error code=` label; hex `0x` plus 2–16 hex digits, 3–8 digits, uppercase alphanumeric codes containing a digit, or `E_`/`ERR_` tokens | Unlabeled codes, ordinary prose, lowercase symbolic values |
| Reference code | English `Reference`, `Reference id/code`, `Ref`, `Ticket`, or `Ticket id` followed by `:`, `=`, or `#`; uppercase token with letters and digits, optional hyphens/underscores | Unlabeled references, guessed product names, numeric-only IDs, ordinary prose |

Supported amount codes: USD, EUR, GBP, JPY, CAD, AUD, CHF, CNY, INR, KRW, NZD, SEK, NOK, DKK, SGD, HKD, BRL, MXN, ZAR. This is a small explicit rule vocabulary, not a live currency registry or financial interpretation. No exchange rates, arithmetic, or minor-unit assumptions are applied.

Labels are case-insensitive; values keep their original spelling. Numeric/date syntax is culture-independent. A URL consumes its full match so a date or email inside it is not emitted as a separate fact. Common trailing sentence punctuation and unmatched closing brackets are removed from URL values; balanced URL brackets are preserved. Every retained character still resolves to its exact source span.

These rules favor precision over recall. They do not implement all address/URI grammars, translated labels, or cross-region reconstruction. Recognizing a string does not verify that a URL is reachable, a mailbox exists, or an error/reference has a particular meaning.

## Budgets and coverage

Default limits:

- Examine at most 2,048 entries, in configured source order and original entry order.
- Include at most 131,072 UTF-16 units, with 32,768 units per entry.
- Retain at most 256 distinct facts and 16 evidence spans per fact; each value is at most 4,096 UTF-16 units.
- Use a 2-second cooperative execution deadline and a 100 ms regular-expression timeout. Cancellation/deadline checks run between entries/matches and during text validation; a currently executing bounded operation can finish before a deadline is observed.

Oversized entries count toward the examination budget and are skipped whole; later entries remain eligible. If the next whole entry exceeds the remaining total character budget, selection stops. Text entries are never concatenated or sliced into partial tokens. Thus a partial URL, decimal, or date cannot be created by budget truncation. Source entry indexes are preserved, including after skipped entries.

The first distinct facts and first evidence locations win. Hitting a fact limit does not prevent gathering later evidence for already retained facts. Excess facts, excess evidence, and oversized candidate values have separate coverage flags. Entries, characters, and oversized-entry omissions also have separate flags.

Coverage records available entry count, included entry count, included character count, and limit reasons. `IsComplete` means all **available declared metadata** was processed without these limits; it is not a claim that the media or all potential scanners were covered. Missing capabilities remain explicit in the dependency snapshot. Source order can prioritize a large OCR result over later QR/transcript entries; limits are visible and the order is configurable.

Persisted coverage must agree with the declared input entry count, referenced evidence entries, minimum characters needed by evidence, and the full character count when all input entries are included. Contradictory counts and unknown limit flags fail closed on load.

## Outcomes and integration handoff

- Present but empty metadata produces successful empty facts with known coverage.
- No declared inputs present returns `Unsupported` / `metadata-input-missing`, without a payload.
- A different processor input contract returns `Unsupported` / `metadata-contract-mismatch`.
- Invalid UTF-16 or NUL in included metadata returns `InvalidSource` / `metadata-text-invalid`, without retaining earlier extracted facts. This describes the metadata input; future worker integration must not infer that the original media file is corrupt.
- Cancellation throws `OperationCanceledException`. Deadline/regex timeout returns `Failed` / `metadata-timeout`. Neither publishes partial success or exception/content text.
- Deliberate capacity limits produce useful partial success with explicit coverage.
- The caller uses the snapshot's derivation when constructing an `AnalysisResult`. Existing store checks reject publication if an input changed in the meantime. Policy, generation/source/run fencing, ordering, retry, progress, and actual scheduling remain in the existing worker and the slice 4 integration work.

## Verification

- Application suite: **412 passed**.
- Infrastructure suite: **287 passed**.
- **69 new cases** cover the extraction corpus, Unicode/evidence, multiple media sources, exact deduplication, missing versus empty metadata, all budget types, malformed data, cancellation/deadline behavior, culture invariance, encrypted round trips, legacy coverage, and invalid persisted coverage.
- The corpus caught and fixed suffix extraction from comma-formatted amounts (`12,50 USD` must not yield `50 USD`). URL punctuation trimming is linear even for long runs of closing brackets.
- Builds use the repository's AOT compatibility analysis and source-generated regex/JSON paths, with no new package dependency. Native executable publishing and real-device provider checks were not rerun; this adapter invokes no model or Windows AI runtime.
- Tests use synthetic representative fixtures. A real-capture usefulness/precision review remains part of integration verification; these results do not establish recall for arbitrary capture content.

Slice 2 is ready for review. The next planned slice is typed titles/summaries/classification and stable local text-provider evaluation. Automatic enrichment remains disabled until slice 4.

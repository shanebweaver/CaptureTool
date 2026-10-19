# Balanced summary inputs and coverage

Semantic processors share a budget across descriptions, recognized text, speech, and selected other metadata. Processing sources in order lets OCR exhaust that budget before any transcript is included. Users also need an indication when a summary covers only part of the available metadata.

## Requirements

Semantic input selection will reserve a fair share of entry and character capacity for each populated source, then reuse spare capacity. Short sources give unused entry reservations to longer sources before sampling. Examine at most 64 entries in total, so rejected oversized entries do not trigger an unbounded search for replacements.

When a source has more entries than its share, sample across its full entry range deterministically. Allocate character capacity to the sample endpoints first, then subdivide the remaining gaps, so a character limit also preserves broad coverage. Restore chronological order within each source before sending the input. Preserve original result IDs and entry indexes for evidence validation. Never cut an entry or fabricate a partial token.

Keep the existing hard limits of 64 entries, 4096 characters, and 1024 characters per entry. Bounded selection must not scan arbitrary amounts of metadata. Keep structured fact extraction's established selection behavior. Record all consulted source identities, including absent and omitted sources, for stale result detection.

Capture details will display a localized notice alongside a generated summary when its input coverage is incomplete. The notice must describe excerpts of available analysis, without implying that complete input selection proves complete coverage of the original media. Empty or complete summaries must not show the notice.

## Acceptance criteria

- A video with 65 OCR entries and one transcript segment includes speech in semantic input.
- Descriptions, OCR, and speech each receive capacity when entries fit their reserved budgets; an oversized source cannot consume another source's reservation.
- Long sources include entries from throughout their range, with stable original evidence indexes.
- Selected entries and characters stay within limits, coverage counts and limit flags are accurate, and cancellation is honored.
- Existing structured fact selection remains unchanged.
- Partial summaries show the notice in all supported locales; complete or absent summaries do not.

## Scope

No larger model context, hierarchical summarization, persisted schema migration, or claim of complete media understanding is required.

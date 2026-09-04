# Capture Memory search projection

## Boundary and ownership

Capture Memory search is a disposable application-layer projection. The canonical source of truth remains the protected Capture Analysis envelopes exposed through `ICaptureAnalysisStore`; the projection never writes metadata or reads image bytes. `ICaptureAssetCatalog` contributes only the preferred-open filename, falling back to the retained-source filename. Full paths are neither retained in the projection nor returned by the search contract. Presentation resolves the current path by `CaptureId` only when a result is displayed or opened.

The concrete `CaptureMemorySearchProjection` implements the search, refresh, and maintenance ports as one process-wide singleton. The worker refreshes an entry after a canonical capability commit, intake refreshes it after a location change, and lifecycle cleanup removes it as soon as an exclusion or forget tombstone is durable. The first query lazily rebuilds the entire projection from protected metadata. Rebuild candidates are published atomically only after the durable control ledger is checked again, so corruption or a concurrent tombstone cannot overwrite the last good projection or resurrect private content.

No OS or vendor content index, vector database, or embedding dependency is used.

## Matching policy

All fields use deterministic Unicode compatibility normalization, invariant Unicode case folding, and letter/digit tokenization. Punctuation and whitespace become token boundaries. Filename, OCR text, and image description remain separately attributable.

Ranking uses these tiers, with only a maximum `0.01` recency tie-breaker inside a tier:

1. Exact filename, then filename phrase
2. OCR phrase, then OCR tokens
3. Image-description phrase, then image-description tokens
4. Filename tokens

Ties are resolved by captured time and then `CaptureId`, so the same corpus produces the same ranking across runs. Typo matching is a lower-scored fallback: a query may contain at most one term with one insertion, deletion, substitution, or adjacent transposition, and terms shorter than five Unicode scalars must match exactly. OCR evidence prefers the matching line and returns its pixel bounds when available. Snippets collapse controls and whitespace, are capped at 1,024 characters, and never contain a directory path.

## Performance baseline

The baseline lab tier is Windows 11 x64, an SSD, at least four logical processors, and 8 GB RAM. Validation uses a Release x64 build, a labeled 1,000-image synthetic corpus, one unmeasured warm-up query, and 30 measured queries. The acceptance threshold is warm p95 below 150 ms. Packaging must use the repository's standard Release x64 MSIX pipeline; the projection code and corpus are platform-independent and run unchanged inside that package.

Run the repeatable service-level gate with:

```powershell
dotnet test tests\CaptureTool.Application.Tests\CaptureTool.Application.Tests.csproj --configuration Release -p:Platform=x64 --filter "FullyQualifiedName~WarmSearchP95"
```

On August 7, 2026, the development lab measured a 2.534 ms warm p95 for the 1,000-image corpus. The packaged smoke check should repeat the same query set after installing the Release x64 package and record the machine tier and p95 in the release evidence.

## Privacy and failure behavior

- Queries, snippets, capture IDs, filenames, paths, and derived content are not logged or sent to telemetry.
- Missing, deleted, unenrolled, excluded, and forgotten Capture Assets are omitted during refresh and rebuild.
- Remove and clear update the in-process projection synchronously; restart rebuilds filter through the durable enrollment ledger.
- A failed rebuild keeps the last good in-memory projection. Canonical envelopes and retained captures are read-only inputs and remain intact.

# Capture Memory UX implementation

Approved September 10, 2026. Work continues on `agent/capture-analysis-stable-identity` alongside the existing unreleased analysis work.

The accepted design keeps one result per capture with two evidence previews, a shared Analysis pane with Matches / All content and persistent search, and paused playback when opening metadata evidence. Search matches retain their source, text, and valid passage/region location. Returning Home restores the search session.

Implemented:

- [x] Shared matching/highlights and multiple bounded evidence passages per capture.
- [x] Query/evidence navigation, source-version checks, and Home search-session restoration.
- [x] Shared Analysis pane with readable text, source filtering, match navigation, copy scope, and distinct content states.
- [x] Result cards with two evidence previews and accessible secondary actions.
- [x] Contextual readiness, Settings scope/availability, and clearer data/troubleshooting controls.
- [x] Focused regression tests, native builds, and UI verification.

Search results now show the actual matched text with consistent source labels: Text in image, On-screen text, Transcript, AI description, and Filename. A result carries canonical passage identity, analysis/source revision, interval or image bounds, and the query into the editor. Filename highlights stay in the capture heading when metadata also matches; passage counts describe metadata occurrences. Filename-only results open normally.

The search projection retains the existing ranking and normalization rules. It emits at most 200 evidence passages per capture with an accurate total, displays two previews, and explicitly labels the 50-capture search limit. Adjacent identical video observations are coalesced. Combined matches spanning multiple passages remain readable without a fabricated location. Highlights use original UTF-16 text positions, including normalized and approximate matches.

All three editors use the same pane and matching semantics. Search persists across sources and Matches / All content. Previous/Next selects and scrolls to a passage; selection, matching characters, and current playback are distinct states. Opening or selecting timed evidence pauses playback and seeks to the available segment timestamp. Copy labels describe the visible scope. Background refreshes preserve unchanged passage instances. Home restores its transient query, selected capture and scroll position, and Back to results uses the existing navigation coordinator so unsaved-edit guards still apply.

The shared native layout uses an inline pane at editor widths of 1,000 effective pixels or more, and a dismissible overlay below that threshold. Pane width is capped at 380 effective pixels and the available width. No separate image/audio/video presentation logic was introduced for the pane itself.

Metadata states distinguish pending analysis, unsupported capabilities, ready-but-empty content, loading failures and unenrolled captures. Cached text remains readable when source locations cannot be verified. Locations are disabled for stale source bytes, edited image geometry or passages outside the current trim; the UI explains why. Image OCR focuses an overlapping text region only when geometry is valid.

Settings exposes the scope of included captures, read-only device capability availability, current work, explicit inclusion of existing captures, and collapsed troubleshooting/data controls. Erasing metadata preserves the current new-capture analysis setting. Model preparation, queuing, and recognition completion are described separately. User-facing additions are present in all six supported resource languages.

Validation:

- Application suite: 656 passed.
- Presentation suite: 313 passed, including guarded return navigation, unchanged-refresh preservation, matching consistency, canonical evidence selection, stale locations, combined passages, copy scope and session restoration.
- Native UI suite: three focused scenarios cover Home setup/search/open/next match/back/remove, Settings lifecycle actions, and the shared analysis pane. These run against isolated test data with synthetic image metadata; they do not download or invoke AI models.
- Native x64 WinUI build passed without compiler warnings or errors. Resource XML, unique keys, and the added/updated UX placeholders were checked across six locales; `git diff --check` passed.
- Native screenshots are produced under `tests/CaptureTool.UiTests/TestResults/artifacts/`: `capture-memory-results.png`, `capture-memory-analysis.png`, `capture-memory-analysis-wide.png`, and `capture-memory-settings.png`.

The screenshot review covers the image search flow in light theme at the test display's scaling, including narrow and wide editor layouts. Real audio/video playback, 200% text, high contrast and a native-speaker translation review remain release verification items; their completion is not implied by these tests.

Timeline marker clustering, resizable panes, semantic search, word-level timestamps, transcript editing, and file relinking remain follow-on work.

Validation baseline: 43 existing application tests covering Memory, metadata, and editor opening passed before these UX changes. Pre-existing working-tree edits are retained. Changes remain on the same branch and have not been committed or published.

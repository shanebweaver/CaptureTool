# Capture experience — step 2: find and reuse capture content

Status: Approved direction; follows step 1.

## Outcome

An image user finds and copies text from the capture. An audio/video user finds a passage and jumps to its timestamp. Long captures remain usable without rendering every metadata field as a card.

## Experience

- Add a Text tab for images/video and a Transcript tab for audio within the shared editor pane.
- Show searchable, selectable recognized text or compact timestamped passages. Video offers a simple source filter for speech and on-screen text; image descriptions are not mixed into literal OCR.
- Search is local to this capture, with a result count and previous/next navigation. Changing a filter preserves the query and makes the active copy scope explicit.
- Copy selected text through native selection, copy an individual passage, or copy the entire filtered text. Do not silently copy only the loaded page when the action says all.
- Selecting timed content pauses playback and seeks to the original source position, correctly respecting trims. Selecting image text locates its source region only when geometry still corresponds to the displayed image. Disabled navigation explains pending edits or unavailable positions.
- Consecutive repeated video text is grouped while retaining original occurrences. Repeated QR values appear once with access to their available positions.
- QR content has explicit Copy and, only for valid HTTP(S) targets, Open link actions. Opening is user initiated, displays the target, and does not interpret other schemes or execute encoded commands.
- Use bounded, incremental presentation for long content; keep full searchable source data outside visual containers. Preserve the active passage and reading position when unchanged results refresh.
- Empty, pending, unavailable, and changed-source states are distinguishable. A sampling/limited-coverage message avoids implying that every frame or spoken word was analyzed.

## Architecture

Project normalized payloads into source passages with stable identifiers, original text, optional time and optional image bounds. Retain source positions through grouping and search. Never fabricate a timestamp or region from a summary or description.

Share matching, filtering and copy-scope behavior across editors. Keep media playback and image-canvas operations in their existing editor owners behind explicit navigation requests. Reuse existing OCR results and preserve the current ad-hoc OCR fallback; do not run duplicate inference merely to populate the pane.

Source verification, metadata deletion and view cancellation continue to gate results. Refreshes should not recreate unchanged passage objects or cause repeated whole-file verification for each progress fraction.

## Acceptance and review gate

- Search and copying work for large image OCR and long transcripts, Unicode text, duplicate observations, no matches, and changing filters.
- Playback seeks correctly with trims; image navigation refuses stale/edited geometry rather than highlighting the wrong area.
- Invalid/non-web QR values remain copyable without becoming executable links.
- Background completion, deletion, navigation away and stale source reads do not restore outdated content or steal focus.
- Test matching/grouping/position contracts and run native editor UI checks; inspect text density and scrolling visually.
- Pause for self-review of user workflows, source fidelity, rendering cost and lifetime behavior; resolve material issues before automatic naming.

## Non-goals

Transcript correction, timeline annotation, semantic search, agent/chat actions, automatic opening of links, tasks/calendar creation and model changes.

# Capture experience review

Branch: `codex/capture-details`, based on `codex/capture-analysis-core`.

## Review path

1. Open an image, audio recording or video in its editor and select **Details** in the toolbar. The shared right pane shows current local file properties even with AI disabled. A wide window reserves space for it; a narrow window uses an overlay. Close or Escape returns focus to the toolbar.
2. With saved analysis, use **Text** (image/video) or **Transcript** (audio). Search this capture, filter by source, copy an individual passage or all matching results, and jump to an image region or recording timestamp. Recording jumps pause playback. Repeated video text and QR values retain their occurrences. Only explicit HTTP(S) QR actions open links.
3. In Settings, enable local AI scanning and **Automatically name new captures**. Capture something new and let its existing analysis produce a title. The name updates in the pane and recent-file label, and Save As suggests a safe filename. Turning the setting on does not rename historical captures. A missing title leaves the ordinary filename in place.
4. Use the pencil beside the capture name to give it your own name. This works without AI. A draft is not replaced by background completion, and a saved user name wins over automatic naming.
5. Delete capture analysis in Settings. Saved names and ordinary local file details remain. Physical files retain their existing names throughout these workflows.

The Home Details dialog has been removed. This work adds no new Home cards or search experience. Existing ad-hoc OCR remains available in the image toolbar.

## Boundaries

The pane reads normalized metadata through `ICaptureDetailsReader`, verifies source identity/revision, rejects deletion-generation changes and cancels on close. Local properties use `IMediaFileDetailsReader` independently of models and consent. Long content has full-data search/copy and incrementally loaded visual rows. Edited image geometry, mismatched working-copy bytes and trimmed-out timestamps disable location jumps rather than using incorrect positions.

`CaptureNamingService` consumes the worker's committed-result notifications. Notifications carry identity and capability, not captured text; they schedule work without delaying inference. Startup recovers eligible committed titles. Names, their user/automatic ownership and intake eligibility are stored in the protected capture catalog. Atomic catalog writes make user changes authoritative and fence disabled or invalidated enrollment. Source leases, authorization leases and deletion watermarks prevent stale publication. No additional inference, plaintext name cache or filesystem rename is introduced.

## Implementation records

- [Step 1: shared pane and independent file details](prd-capture-experience-1-pane.md)
- [Step 2: finding and reusing capture content](prd-capture-experience-2-content.md)
- [Step 3: automatic capture names](prd-capture-experience-3-naming.md)
- [Step reviews and verification](capture-experience-review.md)

## Demo preparation

Use a few real analyzed captures in the actual demo application's data directory: a screenshot with text/QR, a short recording with speech, and a short video. Prepare models before the demo. A title is optional and depends on available analysis; this feature reuses the existing model pipeline.

UI automation uses isolated data and synthetic analysis only when `--capturetool-ui-test` and `--ui-test-details` are present. Its additional `--ui-test-capture` option enrolls the fixture through production intake for the naming lifecycle check. These screenshots demonstrate layout and integration, not model quality or prepared production data.

The final x64 review build is published to `artifacts/pane/app`. Native desktop screenshots are under `tests/CaptureTool.UiTests/TestResults/artifacts/capture-details`, `capture-pane-media` and `capture-naming`. Physical ARM64, offline isolation and spoken Narrator checks remain the documented environment limitations.

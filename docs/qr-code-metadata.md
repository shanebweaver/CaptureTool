# QR code metadata

The editor previously detected QR codes during text extraction, but post-capture
analysis had no QR capability or stored QR results. QR scanning now has its own
`qr-code-detection/v1` capability and `QrCodeMetadata` payload.

The image and video plans advance to `image-v3` and `video-v3`. Their first step
uses `zxing-image-qr` or `zxing-video-frame-qr`, followed by the existing OCR,
transcription, and description steps in their previous relative order. QR
decoding runs locally on the CPU, requires no model preparation/download, and
does not depend on OCR or Windows AI availability. It uses the existing pipeline
for consent, cancellation, source revisions, progress, and protected persistence.

The subsequent [file-details scanner](file-details-metadata.md) advances the image
and video plans to v4 and prepends local file properties before these QR steps.

Each result retains the decoded value, normalized bounds, and a timestamp for a
video-frame occurrence. The same value at different locations or times remains
distinct. A completed scan with no codes is successful empty metadata. Values
such as URLs or Wi-Fi credentials are stored as protected, untrusted data;
analysis never follows links or executes their contents. Metadata deletion uses
the existing delete-all operation.

`QrCodeDecoder` in the common infrastructure layer owns the ZXing implementation.
The editor and analysis adapters both use it. It scans normal and inverted
luminance explicitly, since ZXing's multiple-code API does not apply the
single-code reader's inverted option. Bounds are clamped to the normalized image.

The adapter uses the existing bounded decoder: images/frames have a maximum
dimension of 2048 pixels, videos are limited to two hours, and at most 64 frames
are sampled with a preferred five-second interval. This is sampled video
analysis, so brief appearances between sampled frames or very small/rescaled
codes may be missed. Cancellation is checked between decoding passes and frames;
the pipeline's existing attempt timeout applies to synchronous decoder work.

When the editor reuses saved OCR, it also maps any saved QR bounds to editor
pixels. Complete saved OCR/QR results skip further QR decoding, including a
known empty scan. Older OCR metadata without a QR result still performs ad-hoc
QR detection. An edited image still uses the normal ad-hoc path. Existing
captures get QR metadata through **Analyze existing captures**; an upgrade does
not automatically rescan the library.

Saved and freshly recognized documents use the same application document builder
for reading order, excluding OCR glyphs inside QR bounds, and **Copy all text**.
OCR text is followed by decoded QR values on separate lines. Text without known
bounds stays copyable but has no overlay hitbox. Reusing a complete document does
not append values again, and QR regions remain separate from OCR word boxes.

OCR and QR each distinguish missing results from completed empty scans. Saved
QR-only metadata remains usable when OCR failed or was unsupported; the editor
can obtain consent and retry missing OCR while retaining the saved QR results.
Denied consent, unavailable preparation, failure, or revocation cannot erase those
already stored results. Incomplete documents remain retryable on the next toggle.
Complete metadata bypasses rendering and model work entirely. Original-source
hash validation and edit-revision checks still govern all reuse.

Regression tests cover real encoded QR images, inverted/rotated codes, multiple
locations, synthetic video frame timestamps, empty and invalid media,
cancellation, provider registration, protected storage/reload/deletion, and
editor reuse of old and new metadata. Evidence is under the ignored
`artifacts/qr-metadata/` directory.

The full regression suite passes 945 tests with 94.14% line coverage
(7216/7665). Both desktop workflows pass with the QR step included, covering
the passive progress indicator, settings/deletion, and the editor's QR actions.
x64 and ARM64 Release builds complete without warnings or errors. The x64 Native
AOT publish succeeds with only the four previously accepted Betalgo converter
IL2026/IL3050 warnings. Real image and synthetic video decoding were exercised on
x64; packaged AOT and ARM64 runtime/device verification remain in slice 4.

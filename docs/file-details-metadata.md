# Local file-details metadata

The `windows-file-details` adapter supplies `file-details/v1` for images, audio,
and video. It uses Windows filesystem and media APIs, requires no model or
download, and runs first in the default `image-v4`, `audio-v2`, and `video-v4`
plans. Existing QR and AI steps retain their relative order.

| Media | Properties |
| --- | --- |
| All | Source file name, exact size in bytes, content type when available, filesystem creation/modification dates, and catalog-recorded capture date |
| Image | Original dimensions after EXIF orientation, aspect ratio from pixel dimensions (width/height), and reported horizontal/vertical DPI |
| Audio | Duration, channel count, sample rate in Hz, bitrate in bits/second, and codec |
| Video | Duration, original dimensions after display rotation, aspect ratio from pixel dimensions (width/height), reported frame rate, video bitrate and codec; properties of the first embedded audio track when present |

`FileDetailsMetadata` is a typed payload with optional `Image`, `Video`, and
`Audio` details. Its `MediaKind` must match the containing capture. Dimensions
are original pixel dimensions, never the 2048-pixel analysis thumbnails. The
derived `Dimensions.AspectRatio` describes the pixel grid; it does not apply
non-square video pixel correction. Frame rate is the source API's reported rate,
which can differ from a recording profile's requested rate. Audio details on a
video describe its first embedded track, not all tracks.

Capture date is distinct from file creation date. The application worker reads
the capture catalog once per run and passes its `CapturedAt` through the generic
`AnalysisInput`. Adapters still own no policy, catalog, or metadata persistence.
If there is no catalog entry, capture date stays null; a copied file's creation
date is not substituted. Legacy catalog entries retain their previously recorded
date, which may have originated from recent-history activity. All stored dates
are normalized to UTC. EXIF capture dates are not substituted for the catalog.

The scanner reads properties without rendering images, sampling frames,
transcoding audio, or generating scratch media. The existing worker source lease
and source verification protect the snapshot. The scanner has a one-minute
execution budget; the worker's existing source-size bound still applies. The
two-hour sampling limit used by AI/QR adapters does not apply to this properties
reader.

A missing or inaccessible source returns `InvalidSource`. A readable file with
an unsupported or invalid media header still publishes its filesystem facts.
Unavailable media properties remain null rather than invented zero values. This
lets subsequent analysis steps run independently. Content type falls back to
Windows' file type association when no decoder-reported image type is available;
it is descriptive metadata, not a validation of the file's contents.

The versioned payload uses the existing protected, atomic, source-generated JSON
store. It includes no full source path and inherits source-revision checks,
cancellation, consent, scanning controls, and delete fencing. One confirmed
**Delete metadata** still removes all metadata, including file details. There is
no additional setting, consent, UI, cache, or background worker. Existing captures
can receive these properties through **Analyze existing captures**; upgrading
does not rescan them automatically. Future consumers use the existing
`ICaptureMetadataReader` and select the `FileDetailsMetadata` result.

Tests cover actual encoded PNG/JPEG files (including rotation and dimensions
larger than analysis thumbnails), WAV properties, videos with/without audio,
missing files, unreadable headers, cancellation, capture-date propagation,
media-specific validation, protected round trips, and deletion. Verification
evidence is under the ignored `artifacts/file-details/` directory.

The managed regression suite passes 965 tests with 94.21% line coverage
(7303/7752), including ten native scanner tests with synthetic media.
Both desktop UI workflows pass. x64 and ARM64 Release builds complete without
warnings or errors. An initial ARM64 build encountered an invalid generated
reference assembly; rebuilding that output resolved it without a source change.
The x64 Native AOT publish succeeds with only the four previously accepted
Betalgo converter IL2026/IL3050 warnings. Packaged AOT and ARM64 runtime/device
verification remain in slice 4.

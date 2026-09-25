# Shared local AI consent and saved OCR

This follow-up on `codex/capture-analysis-integration` implements the decision to
use one consent for every local AI feature. It extends the existing OCR tool;
it introduces no editor analysis panels or Home search UI.

## Saved OCR

`ICapturedImageTextReader` is an application service over the capture catalog,
normalized metadata reader, and source hashing service. The editor snapshots the
source revision when opening an image. On first OCR use it looks up the current
metadata, allowing analysis completed after opening the editor to be reused.
Original and preferred paths can identify an unmodified working copy. Ambiguous
identities, changed bytes, unavailable storage, and missing OCR fall back to the
existing ad-hoc path. Hashes are checked against both the editor snapshot and the
stored result; the source lease verifies the file again before returning text.

Normalized word boxes are mapped to the editor's image dimensions. Empty OCR is
a valid saved result. Crops, rotation, drawing, effects, and enhanced images use
fresh OCR of the rendered image. Existing editor results are reused until an
edit invalidates them. A result arriving after an edit or cancellation is ignored.

The Windows text extraction service accepts optional existing text. That path
still decodes the current image to detect QR codes, but does not prepare or run
an OCR model. Reading saved text does not require permission to run new inference.

The [QR metadata follow-up](qr-code-metadata.md) additionally stores QR results.
When both saved OCR and QR results exist, the editor reuses both; older OCR-only
metadata keeps the ad-hoc QR fallback described above.

## One protected consent

The application-owned capture memory service persists the single consent in the
protected policy file. Editor tools access that owner through the shared consent
service; the settings page and first-use flow show the same dialog. Per-tool
settings, dialogs, and the additional super-resolution preparation prompt are
removed. The common dialog includes local processing, stored analysis, and model
downloads.

The four standard settings cards are local AI consent, automatic scanning,
analysis of existing captures, and metadata deletion. Granting consent does not
enable automatic scanning. Disabling scanning preserves consent for interactive
tools. Revoking consent immediately cancels active AI work and prevents late
results from being applied. Regranting creates a new cancellation token; an old
operation stays canceled. Failed protected policy writes fail closed until an
explicit successful retry. Every confirmed Delete still clears all current
metadata; its button is disabled only while deletion is running.

Policy version 2 records the expanded consent scope. Version 1 migrates with
consent and scanning disabled and a new authorization revision, preserving the
capture catalog, metadata, media, and historical enable boundary. Existing users
approve the broader scope once; old per-tool settings are ignored. Migration
must save successfully before the policy becomes available.

## Verification

Regression coverage includes content revisions, working-copy aliases, word box
conversion, empty and unavailable OCR, edited images, cancellation, consent
revocation/regrant, policy failures, and migration. A native encoded-image test
checks that existing text and word boxes survive while QR codes are detected.
Desktop tests exercise the shared consent flow and the four settings controls,
including narrow and dark settings screenshots.

The complete managed suite passes **930 tests**, with **94.09% line coverage**
(7144/7593). Both desktop workflows pass against the normal x64 Release build.
The x64 and ARM64 app builds are clean. x64 Native AOT publishing succeeds with
only the four previously accepted Betalgo converter warnings.

An additional direct launch of the unpackaged Native AOT app timed out before
its main window appeared, both for this build and the preceding
`slice3-vision-app-aot-x64` build. This check does not establish packaged AOT UI
behavior. Package/device verification remains in slice 4; the earlier packaged
AOT provider harness result is separate from app UI verification.

Ignored logs and coverage are in `artifacts/metadata-consent/`. UI screenshots are
in `tests/CaptureTool.UiTests/TestResults/artifacts/`. The UI harness accepts
`CAPTURETOOL_UI_TEST_APP_PATH` to select a published executable for subsequent
package/device checks, retaining isolated fixture data and test providers.

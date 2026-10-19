# AI release readiness

The AI pipeline must preserve source and model ownership through cancellation, report the limits of generated summaries, and stop editor work when the editor closes. These requirements address four reproduced release review failures without adding migration logic or changing the single worker architecture.

## Required changes

| Requirement | Implementation and acceptance criteria |
| --- | --- |
| Bounded shutdown | [Model cleanup and shutdown](ai-bounded-shutdown.md) |
| Source lifetime | [Source protection during native work](ai-source-lifetime.md) |
| Summary coverage | [Balanced summary inputs and coverage](ai-summary-coverage.md) |
| Video editor lifetime | [Video enhancement cancellation](ai-video-editor-lifetime.md) |
| Release evidence | [Native execution and device acceptance](ai-release-validation.md) |

## Release checklist

- [x] Regression tests pass for each requirement, including providers that ignore cancellation.
- [x] All affected managed suites pass in Release configuration.
- [x] Native AOT app and smoke harness publish on x64 and ARM64 with only the reviewed warning allowlist.
- [x] Native recovery and hardware detection execute successfully on x64.
- [x] CI is configured to execute native checks on the matching host architecture and retain their reports; the same script passes locally.
- [x] Packaged x64 speech and vision fixtures produce real output from the required CPU providers.
- [x] Native x64 screenshot and model reuse checks pass their automated acceptance criteria.
- [ ] Manual model output quality acceptance, including inaccurate details in dense OCR suggestions.
- [ ] Packaged x64 and physical ARM64 device acceptance cover real model inference, cancellation, consent revocation, source changes, and repeated use.

Physical device acceptance is a release gate. Cross compiling ARM64 and reporting a model as unsupported or requiring preparation do not establish successful inference on that platform.

## Implementation evidence

The four code fixes are implemented with 17 new regression cases. The seven managed suites pass 1,449 tests in Release: application 447, infrastructure 413, presentation 377, Windows infrastructure 118, Windows editing 52, Windows capture 21, and MCP 21. The application suite also passes after refining sampling under character limits.

The new lifetime tests exercise a hung model unload, worker restart, source protection during timeout and cancellation, shutdown, and late provider failure. Metadata tests cover source reservations, whole entries, evidence indexes, broad sampling, and coverage. Presentation tests cover partial notices and disposal during consent, preparation, generation, and late failure, plus consent revocation.

The CI workflow now calls `test-native-aot-runtime.ps1` on its x64 host and uploads execution reports with publish logs. ARM64 execution remains a physical device check.

The x64 and ARM64 apps and harnesses pass the native warning guard, which permits only the four known Betalgo converter diagnostics. Native hardware detection and all ten recovery cases pass on x64 Windows build 26340 with 64 GiB installed memory. Reports are in `TestResults/ai-release-hardening/native-runtime`; publish logs are in `artifacts/ai-release-hardening/native-aot` under each architecture. The runtime script correctly rejects an ARM64 executable on this x64 host, preserving the distinction between compilation and execution.

Packaged x64 inference passes for Qwen 3.5 0.8B CPU version 3, Nemotron 3.5 streaming ASR CPU version 3, and Whisper Tiny CPU version 4. Both speech providers also pass synthetic silence, video without audio, German, French, and scratch cleanup checks. The required-analyzer gate verifies nonempty output and real model provenance. The report is `TestResults/ai-release-hardening/model-smoke/results.json`.

Native screenshot checks complete all 12 stages across dense code and invoice fixtures, including Phi 4 Mini CPU version 5 naming, summary, alt text, and classification. Model reuse is immediate after an initial 8.4 second load; explicit unload and reload also succeed. Logs are `TestResults/ai-release-hardening/screenshot-checks.log` and `model-reuse.log`.

## Outstanding release acceptance

### Startup stabilization, October 10

Manual testing exposed a gap in the earlier validation: persisted development documents contained removed fields such as `ReconciliationBoundary`, while the JSON contexts rejected every unmapped member. The current control version (2) and catalog version (6) were otherwise valid. This prevented startup from opening the analysis store and blocked new capture registration and OCR admission.

Both JSON contexts now skip extra fields while retaining required-member checks, nullable checks, schema-version validation, and domain validation. There are no field-specific readers or migrations, and startup does not rewrite or reset valid saved data. The infrastructure suite passes 417 tests, including four new cases covering the reproduced startup/OCR failure, first-chance exceptions, and required control fields. The regression failed against the previous readers before the fix.

The new native `--startup-ocr-checks` mode passes on x64 using the actual `windows-ocr-document` provider, generated images, and DPAPI storage. It checks saved identity/name preservation, new capture registration and OCR after restart, and persistence through a second restart. The native publish passes the existing warning guard. Evidence: `artifacts/ai-startup-stability-publish.log` and `artifacts/ai-startup-stability-runtime.log`. Packaged UI acceptance with the rebuilt app remains required.

### Model and device acceptance

Automated screenshot acceptance checks schema, evidence references, broad fixture subject, and category. It does not establish that every generated detail is correct. Manual inspection found that the dense screenshot's suggested name used “Error 2” although the fixture contains error E1042 and a separate count of two problems. The OCR excerpts themselves lost accurate error-code wording. The invoice category was correct, but several generated tags were more specific than the requested broad topics. Review context preservation in OCR inputs and evaluate unsupported detail generation before signing off model output quality.

The Windows AI providers report unsupported on this x64 device. Real NPU image/video enhancement, Windows language model execution, physical ARM64 execution, and packaged UI acceptance remain outstanding. Managed cancellation tests and AOT compilation do not replace those device checks.

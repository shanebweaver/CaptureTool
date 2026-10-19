# Native execution and device acceptance

Managed tests and native compilation cannot establish that native hardware detection, encrypted storage, and real model inference work in a shipped application. Release evidence must distinguish compilation, synthetic checks, and actual model execution.

## Requirements

The native CI job will execute the published harness on a matching host architecture. At minimum it must verify hardware detection and run recovery checks, fail on a nonzero exit, and retain machine readable reports with publish logs. ARM64 cross compilation on an x64 host must remain explicitly separate from ARM64 execution.

Before release, run packaged application acceptance on x64 and physical ARM64 hardware using nonprivate synthetic media. Prepare the expected supported models and require successful inference; preparation required, unsupported, or fallback alone cannot satisfy an expected model's acceptance case.

The provider smoke script accepts `-RequireAnalyzer` with the expected analyzer IDs. Each requested analyzer must produce nonempty successful output with model provenance in every main media fixture. Metadata enrichment checks separately require their fixture acceptance criteria. Keep these gates enabled for release acceptance.

## Device acceptance

Record device architecture, OS, model IDs and versions, package identity, and outcomes for these cases:

- Image OCR, description, summary, classification, alt text, and name generation.
- Audio and video speech recognition and summaries with both visible text and speech.
- Image and enabled video enhancement, including closing the editor during work.
- Consent revocation and clear during inference; no late result or recovered pending request.
- Source rename, replacement, and selected saved copy handling.
- Repeated same model use, model switches, idle unload, and application exit during inference or cleanup.
- Complete and partial summary coverage notices in the packaged UI.

## Acceptance criteria

CI native checks must run against the freshly published executable, fail reliably, and upload reports even on failure. The release checklist must preserve any outstanding physical device or real model acceptance instead of treating a publish or passive smoke result as a substitute.

## Scope

Keep model downloads and hardware dependent checks out of ordinary CI. Do not use private captures, silently enable a disabled feature, or change system wide model configuration for testing.

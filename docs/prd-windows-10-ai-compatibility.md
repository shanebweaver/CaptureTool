# PRD Windows 10 support with optional AI features

Status: Implemented; package/device release qualification pending  
Date: October 2, 2026  
Target: Next release

## Summary

Keep Capture Tool available on Windows 10 while offering newer local AI features on compatible Windows 11 devices. Installing the app, starting it, and using supported capture and editing workflows must succeed when newer AI APIs, runtimes, hardware, or models are unavailable.

Compatibility must be enforced before entering a provider that requires newer Windows components. Show an AI feature only when the device supports a local execution path for it. Unsupported features are hidden, and supported alternatives such as legacy image text extraction remain usable.

## Product decisions

1. Preserve the current application minimum of `10.0.17763.0` in both `Package.appxmanifest` and `TargetPlatformMinVersion`. Keep the newer compilation target and the existing `MaxVersionTested="10.0.26226.0"`. This work does not raise the installation minimum.
2. Determine support per feature and provider. Hide unsupported AI buttons, menu items, tabs, and promotional content. Windows 10 must not be treated as a blanket reason to hide image text extraction or local file details.
3. Keep the current local inference architecture and supported x64 and ARM64 packages. Generative AI on Windows 10 is outside this release's scope.
4. Preserve shared AI consent and the new AI Settings experience. Keep the Video Text, Video Speech, and Audio Speech tabs behind their existing flags, default off. Compatibility checks do not replace rollout flags or restore the removed consent and text-extraction flags.
5. Require evidence from the packaged release build on older Windows. Builds and simulated capability tests alone are insufficient to claim compatibility.

`MinVersion` determines package installation eligibility; raising it would exclude older devices from the entire release. `MaxVersionTested` controls platform behavior and does not provide missing APIs. [Microsoft manifest reference](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-f-targetdevicefamily)

## Problem and current implementation

The application declares an older Windows minimum while including dependencies for newer Windows AI and Foundry Local providers. Microsoft's current Foundry Local Windows guidance requires Windows 11 24H2, build 26100 or later, and compatible x64 or ARM64 hardware with enough memory and storage. Initial preparation can require internet access. [Foundry Local requirements](https://learn.microsoft.com/en-us/windows/ai/foundry-local/get-started#prerequisites)

The new analysis providers already check the OS before probing newer APIs, and the analysis configuration includes legacy Windows OCR. However, several image-edit services still invoke newer API readiness methods directly and catch failures. The application also registers analysis providers during service composition. These patterns need a full audit of construction, discovery, native loading, and execution.

The release package has special handling for overlapping WinML DLL payloads. A guarded method cannot protect startup if a required native dependency fails before that method runs. The actual published and packaged dependency graph must therefore be verified on older Windows.

The current availability contract distinguishes ready, preparation required, unsupported, and temporarily unavailable. It needs enough detail to distinguish unsupported workloads, which must be hidden, from supported workloads awaiting consent or preparation, which should remain discoverable.

## Intended experience

| Environment | Existing capture and editing | Image text extraction | New AI Name and Summary |
| --- | --- | --- | --- |
| Windows 10 within the declared app range | Preserve each feature's existing platform requirements | Show text extraction when legacy OCR is supported | Hide generation actions |
| Windows 11 before 24H2 | Preserve existing functionality | Show compatible text extraction | Hide generation actions |
| Windows 11 24H2 or later without Copilot+ hardware | Preserve existing functionality | Use a compatible Windows OCR provider | Use a compatible configured local fallback when available |
| Windows 11 24H2 or later with supported Windows AI capabilities | Preserve existing functionality | Prefer the configured supported provider | Prefer the configured supported provider, with eligible local fallback |

This table specifies desired behavior, not completed device qualification. An OS version alone never establishes model, language, regional, policy, or hardware availability. Existing feature requirements remain applicable; for example, the current video capture support service requires build 18362 and graphics capture support even though the app minimum is 17763.

Previously saved analysis remains readable wherever the existing storage and consent rules allow it. Viewing results must not require loading the model that produced them. Hide generation and regeneration actions when unsupported, while preserving access to saved text and data management.

## Functional requirements

### 1 Safe startup and provider boundaries

- App startup, Home, Settings, About, opening media, and opening Details must work without initializing unsupported AI runtimes.
- Check known OS and architecture requirements before calling newer native readiness APIs, creating SDK objects, or accessing static members that initialize a runtime. Expected incompatibility must not throw as part of normal discovery.
- Keep newer SDK types inside provider infrastructure. Constructors, shared contracts, UI bindings, and dependency injection factories used on older Windows must not activate incompatible components.
- Use lazy provider factories or supported-platform implementations behind existing abstractions. An unsupported implementation must report its capability status without calling the vendor SDK. Preserve analyzer identities and configuration validation when substituting implementations.
- Audit both the analysis pipeline and existing image/video editing services: text recognition, descriptions, foreground/object extraction, object erase, and image/video super resolution. Each retains its actual requirements; do not assume one minimum applies to every API.
- If an optional DLL is an eager startup dependency, change its loading or package isolation so the older-Windows app can launch. Merely catching exceptions inside an inference method does not satisfy this requirement.
- Passive discovery must not download models, contact a model catalog, start an inference listener, inspect capture content, or load model weights. Resuming an existing authorized request must pass the execution checks below.

### 2 One availability decision used throughout the app

Expose provider requirements and current readiness through application-owned contracts that can be tested without Windows AI installed. UI controls, shortcuts, menus, request admission, and the worker must use the same decision. Track whether a workload is supported separately from whether its model is ready to run.

Feature and provider definitions must declare their platform and workload requirements. Establish support using those qualified requirements and safe, lightweight capability probes. For a local fallback, ship enough compatibility information to recognize a supported fresh device without fetching a catalog or loading its model. Verify actual model availability during authorized preparation. Distinguish a device below a validated hardware minimum from a compatible device experiencing temporary memory pressure.

Represent these conditions distinctly, using stable reason codes rather than localized strings in application logic:

| Condition | Required behavior |
| --- | --- |
| Support not yet established | Keep the feature hidden during bounded discovery; keep ordinary editing usable |
| Supported and ready | Show the feature and allow an explicit action, subject to consent and existing command rules |
| Supported but preparation required | Show the feature; an explicit action can enter consent and preparation |
| Unsupported OS or architecture | Hide all feature entry points and reject execution without invoking the provider |
| Unsupported hardware, language, region, or system policy | Hide the feature for the relevant context; retain the reason in diagnostics |
| Supported OCR engine with missing installable language resources | Keep text extraction discoverable; explain the required resources when the user chooses it |
| Supported workload with temporary unavailability or failed preparation | Keep it visible with appropriate status and recovery; offer retry when useful |
| Probe failed and support remains unknown | Keep the feature hidden; record a diagnostic and allow a later bounded recheck |

Keep consent, rollout flags, and provider readiness as separate inputs. A user who has not granted consent still needs to be able to choose a supported action and reach consent. A feature hidden by a rollout flag must not gain a visible control through this work.

Visibility requires both an enabled rollout flag, where one exists, and at least one compatible local execution path for the feature in the current media context. A compatible CPU fallback is sufficient; having a GPU or NPU is not a universal prerequisite. Model weights need not already be downloaded to establish support. Unknown support must not be represented as supported merely because the OS version meets a minimum.

Evaluate fallback candidates before declaring a feature unavailable. A failed Windows AI probe must not hide usable legacy OCR or a compatible Foundry provider. Do not claim a hardware limitation when the only known fact is an unexpected probe failure.

Cache passive results to avoid repeated probes during layout and navigation. Define invalidation for preparation completion, relevant language/settings changes, and explicit retry. A new app process must reevaluate the environment after an OS update.

### 3 UI consent and localization

- Hide unsupported AI Name, Summary, and image/video editing commands in every surface, including toolbars, context menus, and keyboard discovery. Do not replace them with disabled controls, upgrade messages, empty placeholders, or promotional cards. Preserve manual naming and ordinary editing controls.
- Omit tabs and sections with no supported action or saved content. A section containing retained results may remain available for reading and copying while its unsupported generation actions stay hidden. Hidden controls must be absent from the tab order and accessibility tree.
- Avoid briefly displaying unsupported AI controls during startup or page navigation. Confirm support before revealing them. If support changes, update the UI and move focus safely when a focused control disappears.
- Show AI enablement and benefit content in Settings only when at least one relevant workload is supported. If no workload is supported, retain only controls needed to manage existing consent or saved analysis. Do not advertise features the device cannot run.
- Opening a page must not produce an incompatibility toast, consent prompt, or preparation attempt. A stale shortcut or direct command targeting a hidden feature must perform no provider work.
- Keep image text extraction available through its legacy fallback. Missing installable language resources are a preparation issue when the underlying workload is supported, and must not disable unrelated editing.
- Make consent benefit rows reflect supported offerings. A Windows 10 user enabling image text extraction must not be promised Name and Summary as available features. Features that only need model preparation can still be described as available after download.
- Keep consent revocation, viewing retained results, and deleting saved analysis accessible regardless of current provider compatibility. Granting consent alone does not start analysis.
- Add supported-feature preparation, status, recovery, and accessible text in `en-US`, `de-DE`, `es-ES`, `fr-FR`, `ru-RU`, and `zh-CN`. Check wrapping, larger text, and narrow windows. Keep the media language distinct from the UI language when selecting OCR or speech support.

### 4 Admission execution and recovery

- Check that the requested output has a compatible provider before scheduling expensive prerequisites. Name and Summary continue to declare their own required inputs; hidden Text or Speech tabs must not prevent those prerequisites from running on supported devices.
- Use available source capabilities without requiring every optional source provider to succeed. A video with no speech can still produce a summary from other usable evidence. Preserve the existing suppression of “No speech found” notifications.
- Recheck compatibility before preparation and execution, including persisted requests resumed after restart. Permanently unsupported steps must reach a terminal state with a reason instead of retrying indefinitely or leaving a spinner active.
- Preserve completed results and unrelated supported work when a provider becomes unavailable. Keep source verification, consent checks, deletion fencing, and late-result protection.
- Preparation requires an authorized action or valid authorized resume. Handle offline preparation, interrupted downloads, insufficient storage, and transient runtime failures without blocking capture or editing. Use SDK cache/resume support where available; do not promise offline inference until it is verified with the production runtime and catalog behavior.
- Preserve bounded retries, cancellation, and resource ownership. Finish cleanup before starting a replacement model. Unsupported OS is not a retryable provider failure; cancellation, invalid source, and content-safety refusal retain their existing fallback rules.
- Never substitute a remote inference service. Do not download models for an action already known to be unsupported.

### 5 Packaging and diagnostics

- Validate final x64 and ARM64 MSIX manifests, framework dependencies, runtime assets, native imports, and architecture matching. Verify the published artifact, not only project properties or a Debug checkout.
- Review the .NET, Windows App SDK, Foundry/WinML, and CaptureKit requirements independently. Retaining `MinVersion` alone is not proof that their combined package supports that range.
- Check `systemAIModels` and its ignorable namespace on older Windows. Preserve the newer manifest declarations needed on compatible systems; Microsoft documents both the capability and `MaxVersionTested` requirements for Windows AI imaging. [Image Description packaging requirements](https://learn.microsoft.com/en-us/windows/ai/apis/image-description)
- Log stable capability/provider IDs, reason codes, OS build, architecture, and sanitized error codes through existing diagnostics. Do not log source media, recognized text, prompts, generated results, or raw SDK exception messages that may contain paths or content.
- Expected unsupported discovery must not generate repeated exception or warning output. Genuine unexpected failures remain diagnosable. This requirement does not suppress legitimate cancellation during active work.

## Implementation areas

| Area | Expected work |
| --- | --- |
| [Application abstractions](../src/CaptureTool.Application.Abstractions/Analysis/IMediaAnalyzer.cs) | Add compatible requirement/reason contracts and a shared feature availability result |
| [App service composition](../src/CaptureTool.Presentation.Windows.WinUI/AppServiceProvider.cs) and provider registration | Keep optional native implementations behind safe construction and discovery boundaries |
| [Analysis providers](../src/CaptureTool.Infrastructure.Analysis.Windows) and [edit services](../src/CaptureTool.Infrastructure.Edit.Windows) | Apply the checks consistently to readiness, preparation, and execution |
| [Request admission](../src/CaptureTool.Application/Analysis/CaptureMemoryService.cs) and [worker](../src/CaptureTool.Application/Analysis/CaptureAnalysisWorker.cs) | Use compatible candidates and terminate unsupported work predictably |
| [Details presentation](../src/CaptureTool.Presentation/Features/CaptureDetails/CaptureDetailsViewModel.cs), consent, Settings, and resources | Show only supported offerings and localize preparation and recovery |
| [WinUI project](../src/CaptureTool.Presentation.Windows.WinUI/CaptureTool.Presentation.Windows.WinUI.csproj) and [manifest](../src/CaptureTool.Presentation.Windows.WinUI/Package.appxmanifest) | Preserve the app minimum and verify packaged dependency behavior |

Use the existing adapters, configuration, and availability services where practical. This work does not require a general plugin framework or a second analysis scheduler.

## Verification and release criteria

### Automated checks

- Simulate OS builds immediately below and at each provider's minimum. Assert unsupported factories and native methods are never entered.
- Cover all availability reasons, compatible fallback selection, consent, rollout flags, keyboard/menu entry points, result-only viewing, and rechecking after preparation. Assert hidden controls are absent from navigation and accessibility, and unknown support never briefly exposes an AI action.
- Verify that unsupported Name/Summary requests start no source scans or downloads while legacy OCR remains usable.
- On a supported fresh device with no downloaded models, verify eligible AI features are visible before consent. Discovery must not use model cache presence as a substitute for device support.
- Exercise fresh profiles with missing settings, catalogs, control files, and models. Expected absence must not throw during normal startup.
- Cover persisted work on an incompatible environment, model preparation failure, retry, cancellation, and successful work following a failed request.
- Run existing capture, navigation, editing, analysis, and storage regression suites. Validate resource keys and formatting placeholders for all six languages.

### Device and package matrix

| Target | Required evidence |
| --- | --- |
| Windows 10 build 17763 x64 | Install the release package; cold launch; image capture/edit/save; navigation and Settings; legacy OCR; unsupported AI absent from the UI; existing video-capture limitation |
| Windows 10 22H2 x64 | Fresh install and update from the current release; supported image/audio/video workflows; OCR with and without language resources |
| Windows 11 before 24H2 x64 | Core workflows and OCR; unsupported AI stays hidden without runtime activation |
| Windows 11 24H2 or later x64 without an NPU | Production CPU fallback, first preparation, cached use, interrupted/offline preparation, cancellation, and low-memory behavior |
| Windows 11 24H2 or later ARM64 and Copilot+ hardware | Native ARM64 package and actual inference; native Windows provider availability and fallback where applicable |
| Other advertised OS and architecture combinations, including Windows 10 ARM64 | Install/startup and supported-workflow evidence before claiming that combination is qualified |

Use clean VMs for older-OS installation and ordinary workflows. Use physical hardware for model execution, acceleration, memory pressure, and performance qualification. Test the actual distribution package; repeat loader-sensitive checks for unpackaged builds if those builds are distributed.

Exercise consent, Settings, Details, and capability-driven visibility in all six UI languages. Verify supported but unprepared features remain discoverable and unsupported features leave no empty sections or inaccessible focus targets. Use real OCR/speech fixtures and multilingual Name/Summary checks on compatible devices; distinguish model-output quality from translated UI coverage.

For each run, record the package version, Windows build, architecture, device class, dependency/model versions, setup state, and result. Startup compatibility runs must include expected first-chance exceptions and native load failures in the review, even when a catch handler hides them.

### Definition of done

- [x] The manifest and project minimum remain `10.0.17763.0`, and release artifacts preserve that setting.
- [ ] Required older-Windows package tests pass with optional AI unavailable.
- [ ] Known unsupported paths perform no incompatible native activation, download, or content processing.
- [ ] Legacy image OCR and existing supported capture/edit/save workflows remain usable.
- [ ] Unsupported AI features are absent from all UI surfaces, including consent benefits, empty tabs, keyboard discovery, and accessibility navigation.
- [ ] Supported features remain discoverable before consent or model download, with accessible preparation and recovery in all six languages.
- [ ] Consent, deletion, retained results, feature flags, and independent action prerequisites retain their intended behavior.
- [ ] Required Windows 11 device checks pass with actual providers and bounded recovery.
- [ ] Automated checks and the documented package/device evidence pass before release.

## Delivery sequence and limits

1. Inventory SDK entry points and test the current release package on older Windows. Establish whether any dependency loads before the existing guards can run.
2. Implement shared capability reasons and safe provider boundaries, with legacy OCR regression coverage.
3. Connect admission, execution, consent, and localized UI to those decisions.
4. Validate the final packages across the matrix and record any remaining incompatibilities.

If a dependency cannot be safely isolated on the declared baseline, block release and document the component and failure. Resolve it through loading, packaging, or dependency changes. A higher application minimum would require a separate product decision; do not silently drop Windows 10 support to complete this work.

Out of scope: a new Windows 10 generative inference backend, cloud fallback, automatic GPU selection, model-management UI, changes to the AI output schemas, and expanding the three flagged Text/Speech tabs. Windows lifecycle policy and published support wording should be checked separately from binary compatibility; this PRD specifies the app behavior and evidence required to preserve its existing installation range.

## Implementation and verification — October 2, 2026

### Implemented behavior

- `IAnalysisFeatureAvailability` evaluates the configured provider candidates and the inputs declared by each output. Name, Summary, and Alt Text need a supported output provider and at least one supported source. Rollout flags continue to control the three unreleased media tabs independently.
- Device support is independent of consent and model installation. Unknown support stays hidden. A previously supported provider remains discoverable during transient probe failures; a definitive unsupported result hides it. Shared probes run away from the UI thread, have a three-second deadline per provider, and cache feature results for one minute. Settings refresh and explicit requests invalidate the cache. Diagnostics record provider IDs, readiness/reason codes, OS version, architecture, and sanitized HRESULTs.
- Windows AI entry points check the platform before querying the SDK, preparing models, or reading source media. Existing description, foreground extraction, object removal, and super-resolution services use the same platform gate. Missing service registrations no longer expose those tools.
- The Foundry CPU path requires Windows build 26100 or later, native x64 with AVX2 or native ARM64, and at least 8 GiB of installed memory. It continues selecting CPU variants explicitly. This is a conservative eligibility profile; model performance at the memory floor still needs device qualification. The [Microsoft Foundry Local workshop requirements](https://github.com/microsoft-foundry/Foundry-Local-Lab/blob/main/README.md) specify an 8 GB minimum and AVX2 for CPU execution; the [Windows SDK requirements](https://learn.microsoft.com/en-us/windows/ai/foundry-local/get-started#prerequisites) establish the OS baseline and compatible CPU fallback.
- Legacy OCR bypasses Windows AI on older systems. Missing OCR language resources remain a recoverable setup state, with guidance in all six languages. Such steps terminate without repeated preparation attempts.
- Unsupported generation controls and empty tabs are removed from the UI. Saved text, summaries, descriptions, and name suggestions remain readable. Direct requests and shortcuts also check support before consent or scheduling.
- Consent shows only supported benefit rows. Settings hides AI enablement when no advertised analysis feature is supported, while preserving consent revocation and deletion of retained data. Consent alone still schedules no work.

### Automated evidence

Run on Windows build `10.0.26340.0`, x64. UI tests use isolated data and deterministic providers; they do not establish native inference support on simulated devices.

| Check | Result |
| --- | --- |
| Application tests | 442 passed |
| Infrastructure tests | 394 passed |
| Presentation tests | 370 passed |
| Windows analysis tests | 107 passed |
| Windows editing provider tests | 52 passed |
| WinUI compatibility, consent, deletion, and generation tests | 14 passed |
| Localized OCR-only consent | All six languages passed; screenshots inspected for wrapping and clipping |
| Release MSIX, x64 and ARM64 | Built; static package validation passed |

The new tests cover missing providers, preparation-required states, missing language resources, partial prerequisites, bounded/failed probes, recovery, unsupported direct requests, retained results, unknown visibility, and the asynchronous text-scan command. Native-boundary tests confirm that a rejected device cannot prepare Windows/Foundry models or touch source storage.

Both `2.16.3.0` MSIX artifacts retain minimum `10.0.17763.0`, maximum tested `10.0.26226.0`, the ignorable `systemai` namespace, and all six resources. The app, CaptureKit, Foundry, WinML, and ONNX payloads match their package architecture. The native AOT executable import tables list Windows/UCRT libraries without direct dependencies on optional AI DLLs. This does not establish compatibility of every dynamically loaded or transitive dependency.

The packaged framework dependencies are Windows App Runtime `2.5.1.0`, Store Engagement `10.0.23012.0`, and VCLibs `14.0.33519.0`. Their supplied x64 and ARM64 package manifests declare minimums of `17763`, `10240`, and `10042`, respectively, so none raises the app's declared installation minimum. Both AOT builds retain the four previously documented IL2026/IL3050 warnings in the vendor `Betalgo.Ranul.OpenAI.Error.MessageConverter`; no new warning category was introduced.

Repeat the static package checks with:

```powershell
./tools/Validate-AiCompatibilityPackage.ps1 -PackagePath <x64.msix> -Architecture x64
./tools/Validate-AiCompatibilityPackage.ps1 -PackagePath <arm64.msix> -Architecture arm64
```

Local evidence is under `artifacts/ai-compatibility/` and `tests/CaptureTool.UiTests/TestResults/`. Packages are unsigned validation artifacts and were not installed over the user's app.

### Remaining release qualification

The older-Windows and physical-device matrix above remains required. Hyper-V denied VM inventory access on this workstation, including outside the filesystem sandbox, so Windows 10 package installation, cold startup, native loading, and OCR were not exercised here. ARM64 was cross-compiled and inspected, not executed. Validate both the minimum-memory CPU profile and Copilot+ hardware, along with offline/download recovery, before declaring the release qualified. The existing manifest minimum is preserved; these automated checks do not replace that device evidence.

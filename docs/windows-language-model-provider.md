# Windows language-model provider

The preferred semantic provider is `windows-language-model` for synopsis/title,
classification, and image alt text, followed by the Foundry Local CPU `phi-4-mini`
backup. `MetadataEnrichmentConfiguration.TextModels` is the configuration point.
The backup restores these actions on the tested i9-9900K / GTX 1080 Ti, where
Windows reports `NotSupportedOnCurrentSystem`. This supersedes the earlier
Windows-only decision. Image-description and speech providers are unchanged.

Both providers run only after an explicit action under the shared local-AI
consent. Saved metadata is reused, providers run sequentially, and the Foundry
model unloads when requested work drains. This restores compatibility, not lower
peak memory: Phi-4 can still use roughly the 8 GB observed during testing.
Strict validation and bounded correction remain in place; see the
[screenshot stability investigation](metadata-enrichment-stability.md) for the
current version 3 transport and the earlier prepared-metadata qualification.

The provider uses the existing stable Windows App SDK packages and
`Microsoft.Windows.AI.Text.LanguageModel`. It does not pin a Phi Silica model name,
download model weights itself, or change Windows channels. Hardware support and
the installed model are controlled by Windows. See Microsoft's
[requirements and access guidance](https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica).

## Execution contract

- The existing shared local-AI consent and explicit action authorize execution.
  Registration and readiness checks neither load models nor call `EnsureReadyAsync`.
- The requested semantic capability is checked before enqueueing its OCR/image
  description prerequisites. If every configured candidate is unsupported or
  temporarily unavailable, the action shows the existing unavailable notification
  without expensive prerequisite work. A ready or preparable backup permits the
  request, even if Windows is unavailable.
- The worker prefers Windows and reaches Phi-4 only if the preferred candidate
  cannot complete the request. A successful Windows result never loads Phi-4.
  Content-policy refusals and cancellation do not trigger fallback. Native calls
  that outlive cancellation retain ownership until they return before any other
  model can run.
- Authorized preparation can call Windows `EnsureReadyAsync`. A Foundry model left
  resident by an image-description prerequisite is released before creating the
  Windows language-model session.
- Each action owns a disposable Windows model session. A correction reuses that
  session, but every attempt starts with a fresh context. Application instructions
  use `CreateContext(systemPrompt)`; quoted metadata is supplied separately as the
  user prompt. No context or source text carries into another capture.
- Input selection, evidence mapping, output bounds, and strict JSON parsing are
  shared with the Foundry adapter. At most one correction is allowed within the
  existing execution deadline. Policy/content blocks, unsupported language, and
  context overflow are not retried. Context overflow is reported rather than
  silently truncating sources or changing evidence identities.
- Native creation/generation is awaited until completion. Cancellation prevents
  publication and further attempts; it never abandons an operation and starts
  another model on top of it. The session is disposed when the operation returns.
- The adapter uses the base `GenerateResponseAsync` API with default Windows
  content filters and temperature zero. It requests JSON and independently validates
  it; it does not claim constrained decoding. The documented structured-output
  schema subset differs from our nullable suggestion schema, so that API requires
  a separate qualification on supported hardware before adoption.
- Provenance identifies `microsoft-windows` / `windows-language-model`. The SDK does
  not expose the actual underlying model version here, so it remains unspecified.
  Logs never include capture text, generated content, or access credentials.

## Microsoft access credentials

For a runtime that requires Microsoft's Limited Access Feature token, supply the
Microsoft-issued app token and matching attestation at the existing host
composition call:

```csharp
collection.AddWindowsAnalysisProviders(
    MetadataEnrichmentConfiguration.SemanticModels,
    languageModelAccess: new WindowsLanguageModelAccess(issuedToken, appAttestation));
```

No token is bundled or invented. Unlocking is attempted only during authorized
preparation. Access denial maps to unavailable. Leaving credentials unspecified
allows runtimes that do not require a token to work through their normal APIs;
it does not bypass access restrictions on runtimes that do.

## Validation and remaining hardware check

All 43 targeted Windows/Foundry adapter tests passed, covering fresh sessions,
bounded correction, evidence/provenance, cancellation ownership, refusal/status
mapping, and access denial. All 351 infrastructure tests and ten configuration
tests passed. The new integration test verifies that an unavailable semantic
provider does not enqueue OCR or image description.
The x64 native release publish passed with the existing documented Foundry SDK
AOT warnings and no new Windows-provider warnings.

Restoring the backup adds configuration and integration coverage for provider
order, unsupported Windows readiness, preparing a fallback only when needed, and
admitting explicit requests when the backup is ready or needs preparation. All
145 focused configuration, workflow, and Windows/Foundry provider tests passed.
That configuration-only restoration did not rerun the long native inference corpus.
The subsequent screenshot investigation changed the generation contract to version 3
and adds native checks from real OCR and image-description outputs.

`CaptureTool.Analysis.Smoke <output-directory> --windows-language-probe` performs a
passive native readiness check without preparation or inference. The unpackaged
probe returned `CapabilityMissing`; no model was prepared or loaded. Windows
requires package identity with the `systemAIModels` capability, already declared in
CaptureTool's package manifest. Test actual generation using the packaged app.
Repeating the passive probe under CaptureTool's installed package identity returned
`NotSupportedOnCurrentSystem` on this PC. No model preparation or inference ran.
The earlier missing-capability result was specific to the unpackaged harness.
The existing `--enrichment-checks` diagnostic also accepts Windows processors, but
requires that same package capability to evaluate them. That diagnostic explicitly
permits preparation; use the passive probe for a check without downloads.

This workspace's i9-9900K / GTX 1080 Ti cannot qualify actual Windows language-model
generation, repeatability, latency, or memory use. Those measurements remain a
required hardware validation before claiming production output quality or a
particular RAM saving. ARM64 generation is also unverified.

Unavailable-model preflight uses `model-unavailable`, separate from the worker's
`provider-unavailable` busy/native-shutdown state. The details pane shows the
localized device-unavailable message and inline action status, rather than a
misleading unreadable-details error. Readiness diagnostics log the native state or
exception type/HRESULT only. Missing metadata documents are still handled as empty
reads; first-chance missing-file/directory messages in a debugger do not themselves
mean an analysis failed.

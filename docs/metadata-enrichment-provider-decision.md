# Metadata enrichment local text provider decision

Status: selected for the initial semantic configuration after local x64 evaluation. See [verification and review](metadata-enrichment-verification.md).

Use the existing stable `Microsoft.AI.Foundry.Local.WinML` **1.2.4** dependency with the CPU `phi-4-mini` alias. The evaluated resolved model is **`Phi-4-mini-instruct-generic-cpu:5`**. Record the actual model/version on every successful result; an alias is not provenance. The model weights occupy about 5.6 GB in the isolated test cache. Preparation can download models/runtime components under the existing local-AI consent.

[Microsoft's model card](https://huggingface.co/microsoft/Phi-4-mini-instruct) describes a 3.8B parameter multilingual model with an MIT license. [Foundry Local's stable Windows guidance](https://learn.microsoft.com/en-us/windows/ai/foundry-local/get-started) documents Windows 11 24H2+, x64/ARM64 CPU support and initial online preparation. This provider does not require Copilot+ hardware or a limited-access token. Native ARM64 runtime and actual offline operation remain unverified here. Phi Silica's [limited-access requirement](https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica) excludes it from this delivery.

## Execution and output

- The application-owned ephemeral loopback chat endpoint receives bounded metadata only, with redirects/proxies disabled, no tools, and response storage disabled. No capture paths, media bytes, source text, prompts, or raw responses are logged by the adapter.
- Use constrained JSON (`response_format` / `json_schema`) plus independent parser/domain validation. The local SDK documents [JSON schema response formats](https://github.com/microsoft/Foundry-Local/blob/main/sdk/rust/docs/api.md); real execution with this installed version is recorded separately.
- Models cite numbered input entries. Code resolves those numbers to exact stored result identities, original entry indexes, and full-entry UTF-16 spans. No fuzzy quotation matching or model-generated offsets. Evidence granularity is the bounded source entry, not an independently verified factual claim.
- `capture-synopsis` contains an optional title and up to three summary statements. `capture-classification` contains an optional category from vocabulary v1 and up to five lowercase topics. Both may abstain. These are inferences, separate from literal structured facts and user-authored titles.
- Input order is OCR, transcript, description, QR. Bounds: 64 examined entries, 4,096 characters total, 1,024 per whole entry; oversized entries are omitted and coverage records that limitation. Output limit: 1,024 tokens / 64 KiB transport; two-minute execution and ten-minute preparation budgets per step.
- Native ownership continues until inference returns even after cancellation. The existing worker fences publication immediately and prevents overlapping provider calls; the adapter then stops its listener and unloads the model.

## Evaluation and limitations

Initial free-form/quotation-copying protocols were not reliable enough. Qwen `qwen3.5-0.8b-generic-cpu:3` omitted evidence; `qwen2.5-1.5b-instruct-generic-cpu:4` and `qwen3.5-2b-text-generic-cpu:1` also produced wrong categories or failed abstention. Phi initially produced malformed JSON. Simplifying citations and constraining the output format resolved that mechanical problem in the measured Phi run. The Qwen candidates were not requalified against the final protocol; this is a workload/configuration decision, not a general ranking of those models.

Only Phi is configured initially. Candidate ordering and fallback remain generic and tested. A second semantic candidate can be added in `MetadataEnrichmentConfiguration.TextModels` after passing the same corpus and runtime checks. If Phi is unavailable, first-level results and deterministic facts remain available; there is no cloud fallback or forced low-quality semantic fallback.

Review meaning separately from valid schemas: the measured summaries preserved invoice values, an undecided plan, missing-file errors, contradictory statuses, and limited source coverage. Noise abstained and hostile input either abstained or was rejected before publication. German input sometimes yielded English summaries. Categories/topics are coarse suggestions: `next week meeting` was broader than the conversation's actual `compare options next week`, and conflicting status entries were categorized `error`. Consumers must not turn these inferred labels into observed facts, obligations, appointments, or automatic actions. A larger real-capture precision review remains prudent before adding user-facing consumers.

No new presentation surfaces, consent/settings, package dependency, automatic rename, or action execution are introduced.

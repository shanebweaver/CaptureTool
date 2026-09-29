# Compact input for local metadata generation

## Change

OCR supplies individual words. The previous model input repeated `id`, `kind`, and
`text` around every word. The compact format groups sources by capability and
uses each source ID as a JSON property name:

```json
{
  "complete_available_metadata": true,
  "sources": {
    "description": {"0": "An invoice with a total and payment deadline."},
    "text-recognition": {"1": "Northwind", "2": "invoice", "3": "INV-1042"}
  }
}
```

The prompt explains the numeric keys. Serialization preserves every selected
entry's text and global ID, including Unicode, whitespace, punctuation, duplicate
words, and embedded instructions. Source data remains JSON quoted and separate
from system instructions. Evidence still resolves to the original result, entry
index, and UTF-16 span. Selection limits, coverage, strict output validation, and
the single correction allowance stay the same.

Inputs with fewer than four selected entries retain the previous per-entry format
and prompt. In the experiment, the extra format instruction outweighed the saved
structure for one or two excerpts. The threshold uses the selected entry count,
so skipped oversized entries cannot incorrectly trigger compaction.

This applies to both Foundry and Windows language-model adapters through the
shared protocol. Processor contract versions advance to name 2, summary and
classification 5, and alt text 4. The saved payload schema does not change.

## Evaluation

The opt-in smoke diagnostic `--input-compaction-checks` compares the production
compact format with a reconstruction of the previous format and prompt. It uses
the cached `Phi-4-mini-instruct-generic-cpu:5` model on this PC (i9-9900K), holding
one model lease throughout. Both formats use identical source selection, model
settings, response schemas, parsing, and retry policy. Two rounds reverse format
order and measure name, summary, and alt text on synthetic invoice and dense OCR
fixtures. Generation time includes request construction, inference, parsing,
diagnostic writes, and any correction; model loading is recorded separately.

Additional paired fixtures cover German, embedded instructions, conflicting
sources, skipped oversized entries, description-only alt text, and classification.
Automated checks cover parse success, evidence resolution, and basic expected
subjects or abstention. The saved outputs also need a meaning/citation review;
schema validity alone does not establish factual accuracy.

### Measured results, September 28, 2026

Means of two calls per format and action, in seconds:

| Fixture | Action | Previous | Compact | Time reduction | Prompt tokens, previous → compact |
| --- | --- | ---: | ---: | ---: | ---: |
| Invoice | Name | 11.87 | 8.60 | 27.6% | 460 → 386 |
| Invoice | Summary | 13.94 | 11.46 | 17.8% | 493 → 419 |
| Invoice | Alt text | 12.66 | 11.28 | 10.9% | 488 → 414 |
| Dense OCR | Name | 23.81 | 12.93 | 45.7% | 1,245 → 631 |
| Dense OCR | Summary | 28.67 | 16.55 | 42.3% | 1,278 → 664 |
| Dense OCR | Alt text | 27.68 | 17.01 | 38.5% | 1,273 → 659 |

The dense fixture's 64 entries took 3,554 JSON characters in the previous format
and 1,164 in the compact format. Both contain exactly the same selected text.
The invoice's ten entries shrank from 627 to 289 JSON characters. Prompt counts
include instructions; output length also varies with the format. For dense OCR,
output counts stayed close (name 17 → 16, summary 41 → 38, alt text 41 → 43),
supporting reduced input processing as the main source of the speedup. Cold model
loading took 8.65 seconds and is excluded from the table.

All 36 native calls produced schema-valid responses and valid evidence references
on the first attempt. All 24 core performance calls retained the expected subject;
the two rounds produced identical text for each format and action. Quality checks
passed for German, the skipped oversized entry, and description-only alt text.
Classification selected Document in both formats.

The initial experiment forced compaction even for tiny inputs. A single excerpt
gained 12 prompt tokens; German, the skipped-entry fixture, and the photo took
0.42–0.67 seconds longer. A two-excerpt conflict gained one token, and its compact
output called the status "undecided" instead of explicitly identifying the
contradiction. These inputs now retain the previous format and instructions;
the measured compact requests for invoice and dense OCR are unchanged.

Three automated fixture checks failed in that experiment: both hostile-source
outputs described the embedded instructions instead of abstaining, and the compact
conflict output failed the explicit-conflict check. The diagnostic therefore
returned exit code 1 despite all transport/schema checks passing. Manual review
also found proper-name tags in both classification outputs, contrary to the prompt.
These existing abstention/tagging weaknesses remain model-quality issues; this
change does not claim to fix them. Retaining the previous format for tiny inputs
avoids adopting the observed conflict wording regression.

Raw results are under
`artifacts/slice4/provider-smoke/input-compaction/20260928-195043`.
The diagnostic now compares the adaptive production format with the previous
format, so repeating it uses identical requests for the tiny fixtures.

### Code verification

- 100 Windows analysis tests passed, including format selection, exact Unicode
  round trips, global source IDs across capabilities, and evidence resolution
  after omitted entries. Existing malformed-input, output-schema, correction,
  cancellation, and Windows-adapter tests also passed.
- 82 application analysis tests passed with the updated processor versions.
- The final x64 diagnostic build passed with zero warnings and errors.
- The native comparison above ran before adding the tiny-input threshold.
  The final threshold selects between the same two evaluated formats; no further
  long native run was needed. Unit tests cover the threshold and matching prompts.

## Scope

This reduces prompt processing work. It does not reduce cold model loading time
or change the existing on-demand actions and 30-second model retention window.
Results from small synthetic fixtures on one machine are not a latency guarantee.
Native Windows language-model inference is unverified on this unsupported device.

Compaction removes repeated structure, not capture content. It still sends the
same bounded selection of at most 64 entries and 4,096 text characters. Joining
OCR into lines or paragraphs could further reduce overhead and improve coverage,
but would need an explicit mapping from each grouped citation back to multiple
original OCR regions. That is a separate change to the input/evidence contract.

## Reproduction

```powershell
& tools/CaptureTool.Analysis.Smoke/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/CaptureTool.Analysis.Smoke.exe "$PWD/artifacts/slice4/provider-smoke" --input-compaction-checks
```

Build the harness first and use an output directory containing the cached CPU Phi
weights. This check never downloads models or reads user captures. It writes raw
synthetic requests, responses, and `results.json` under `input-compaction` in the
output directory. The console log for this investigation is
`artifacts/input-compaction-run.log`.

# Capture Analysis Inspector

The Capture Analysis Inspector is a Debug-build view over the protected canonical metadata store.
Open **Debug > AI Model Lab > Open Capture Analysis Inspector**. The inspector enumerates records
through `ICaptureAnalysisQueryService`, joins the current Capture Asset only for its filename and
path, and renders normalized app-owned metadata. It never reads provider responses or bypasses the
current-user protection on the envelope files.

The readable JSON includes source identity, recipe requirements, canonical results, terminal
outcomes, dependency references, analyzer/model provenance, and every supported payload field.
OCR words and geometry, transcript segments and timestamps, and video observations are preserved.
The JSON is a diagnostic projection; the protected `.analysis` envelope remains authoritative.

**Copy JSON** and **Export JSON…** are explicit plaintext disclosures. Exported files may contain
private text extracted from a capture and are not enrolled in lifecycle cleanup, search indexing,
or reanalysis. Automatic adjacent sidecars remain intentionally unsupported. If user-facing
metadata portability is added later, it must use a separately consented export policy and must not
become a second canonical store.

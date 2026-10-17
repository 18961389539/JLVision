# Industrial recognition dataset acceptance (JLVision)

This benchmark is for **labeled production-image verification** of the optional
managed `JlBarCode`, `JlDataCode2D` and Tesseract `JlOcr` adapters.
It does not validate unverified native barcode/OCR operator IDs.

## Setup

Copy `docs/recognition-dataset-manifest.template.json` to a PRIVATE
dataset directory and replace every placeholder, especially exact expected
symbols/text and the real 40-character Git SHA. Create the referenced image
files in the same directory or a child `images/` folder. Only local relative
paths inside this directory are accepted.

**The template is not a runnable dataset.** Its six example frames cannot
meet the suggested acceptance minimums. CI runs independently generated
synthetic positive and negative fixtures; no customer data is committed.

Every frame must have:

- `id`: unique stable label;
- `file`: relative image path;
- `kind`: exactly `barcode1d`, `datacode2d` or `ocr`;
- `condition`: independently annotated lighting/blur/rotation/reflection category;
- `expectedPresent`: explicit `true` / `false`;
- for positive barcode frames: `expectedText` and `expectedFormat`
  (e.g. `CODE_128`, `QR_CODE`, `DATA_MATRIX`);
- for positive OCR frames: exact `expectedText`. OCR uses case-sensitive,
  unnormalized matching. Negative frames must NOT specify text/format;
- optional `roi` of column, row, width and height in full-image pixels.

Each sample ROI must contain **at most one annotated expected symbol**.
Additional decoded symbols are considered incorrect, not implicitly OK.
The current OCR benchmark uses single-line segmentation; prepare each OCR
ROI accordingly, preserving sufficient white margin.

## Execute on Windows x64

```powershell
dotnet run --project tools/JLVision.RecognitionBenchmark/JLVision.RecognitionBenchmark.csproj --configuration Debug -- `
  --manifest "D:\vision-acceptance\manifest.json" `
  --output "D:\vision-acceptance\benchmark.json" `
  --tessdata "D:\vision-ocr\tessdata" --language eng
```

Omit `--tessdata` and `--language` for datasets that have no OCR frames.
The specified Tesseract language model must exist in the tessdata directory.
Every image is read with the current `JlImage` native runtime (Windows x64).
Use **single-channel 8-bit grayscale images**; do not rely on silent
conversion of color inputs. Decode latency includes image file reading,
image-to-Gray8 copying, crop and decoding, and for OCR includes its
in-memory image conversion. It is NOT whole robot-cycle latency.

## Metrics

| Metric | Definition |
|---|---|
| True positive | One decoded symbol/text exactly matches the independently labeled target (and barcode format) |
| False negative | Positive frame without an exact accepted match (includes misreads) |
| Misread | Positive frame produced content, but wrong text/format or extra symbols |
| False positive | Negative frame produced any nonempty content |
| True negative | Negative frame produced no decoded content |
| Miss rate | FN / all positive frames |
| Misread rate | Wrong-content positive frames / all positive frames |
| False-positive rate | FP / all negative frames |
| P95 | Nearest-rank 95th percentile of per-frame elapsed milliseconds |
| Processing error | Unreadable image, wrong pixel type, ROI error, decoder exception, etc.; **always fails the gate** |

Acceptance is evaluated per required algorithm family and per required
condition. Each required family MUST have enough positive and negative
frames. Required conditions must be present and can also impose minimum
per-condition positive/negative counts. Rates with zero denominators are
not silently accepted: minimum-sample gates determine whether a bucket
with no positive/negative samples is permitted.

## 95% uncertainty intervals and optional conservative gate

Every overall/algorithm/condition/algorithm×condition metric reports a
**two-sided Wilson score 95% interval** for correct-read, miss, misread and
false-positive proportions. The interval has `events`, `trials`,
`estimate`, `lower95` and `upper95`. When a denominator is zero, the
estimate and interval fields are `null` (displayed as N/A), never 0%.

By default, the existing software acceptance gate uses **observed point
estimates**, plus explicit minimum sample counts and processing-error checks.
To also require that each error-rate **Wilson upper 95% bound** stays below
the configured maximum, add to the manifest:

```json
"acceptance": {
  "requireWilsonUpper95": true
}
```

This is intentionally stricter: every required algorithm, condition and
algorithm×condition bucket must contain **both positive and negative** images.
For example, zero misses among 10 independently labeled positives still has
a two-sided 95% Wilson upper bound of about **27.8%**. Zero observed errors
is therefore not statistical proof of a zero true error rate. On a roughly
1% upper-bound goal with zero observed errors, hundreds of independent trials
are needed. The minimums and threshold policy should be approved by quality
engineering, with attention to correlated frames, selection bias, and multiple
comparisons. The Wilson interval is not a guarantee of real-world error rates.

### Visual HTML acceptance report

The same JSON can be rendered **offline** as a static, self-contained HTML
acceptance report with a high-level verdict, configurable gates,
algorithm/condition matrices, Wilson interval bars, P95, sample counts,
privacy-preserving failed-frame diagnostics (IDs and error categories only), and
traceability. No customer images, recognized customer text, external scripts,
remote fonts or network resources are embedded.

```powershell
python tools/recognition-report.py --input "D:\\vision-acceptance\\benchmark.json" --output "D:\\vision-acceptance\\benchmark.html"
```

The generated HTML is a review artifact, **not a signed certificate**.
The `sourceType` / commit are user-declared and require independent
verification, and `realFieldAccuracyCertified` remains false.

Zero misread and false-positive thresholds are **example policy**, not a
statistical proof of a true zero field error rate. For meaningful claims,
choose adequate sample sizes and confidence bounds with quality engineers.

Exit codes: `0` = configured software metrics passed, `2` = gate failed,
`1` = malformed inputs, incompatible environment or dataset runtime error.
Output is machine-readable JSON with per-frame IDs, image SHA256, overall
and grouped confusion counts, P95, and explicit failed limits. **Raw
decoded customer strings are not copied into the JSON report.**

`sourceType="synthetic"` reports `SYNTHETIC_SOFTWARE_GATES_PASSED` and
does not certify real accuracy. `sourceType="field"` reports
`SOFTWARE_GATES_PASSED_HUMAN_REVIEW_REQUIRED` when software gates pass.
The source type and software commit are **declared metadata, not independently
verified provenance**. Every report has `realFieldAccuracyCertified=false`.
An independent reviewer must verify raw images, trusted annotations, real
equipment measurements, sample representativeness and final acceptance.

The CI benchmark generates deterministic Code128, QR and DataMatrix images
and blank negatives. It also tests wrong-text misreads, false positives,
missing frames, minimum population and nearest-rank P95. OCR is verified
by the separate optional Tesseract CI with a synthetic rendered label.
A production OCR dataset is still required for industrial validation.

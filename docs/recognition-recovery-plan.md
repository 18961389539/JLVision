# Industrial recognition recovery: evidence and staged acceptance

This repo currently exposes **traditional pattern/shape detection** and some
text-region/OCV primitives. The current public managed source inventory does not
include the complete 1D barcode, 2D data-code or OCR recognition wrappers.
The repository contains old removal-rule scripts mentioning those families,
but Git history in this repository does not prove when or whether the original
managed classes were removed.

## What has been checked

- `tools/audit-recognition-inventory.py` inspects current public C# method
  signatures, removal-rule scripts and the bundled DLL's static name strings.
- `tools/JLVision.RecognitionProbe` queries the native `HLILogicalName`
  registry **without executing an operator** on Windows x64.
- The presence of a string or native logical name **does not establish** the
  correct argument slots, memory ownership, native license rights or reliable
  recognition with customer images. Missing names do not prove absence.
- All artifacts must record the exact managed commit and native DLL hash.

## Recovery decision sequence

1. Determine which native operator names/IDs actually exist and whether the
   DLL's ABI corresponds to current interop `HLICreateProcedure` and IO slots.
2. For each operator, confirm its complete input/output slot contract and
   explicit ownership rules from authoritative SDK/header/source evidence.
   **Never guess an operator ID or signature from a string hit.**
3. Implement small typed wrappers in isolation, preserving legacy public API.
   Start with model lifecycle (create/load/save/close), then decode/read/clear.
4. Add Windows x64 native positive and negative fixtures for Code128, QR and
   DataMatrix; include invalid inputs, damaged symbols, orientation changes,
   resource-disposal loops and concurrent independent workers.
5. Apply equivalent OCR fixture coverage to single-character, word and OCV
   recognition before describing them as available industrial identification.
6. Publish decoding correctness, error recovery, allocation growth, P95 latency
   and external real-scene acceptance, separate from synthetic examples.
7. Only then mark a family as **Native validated** in the capability matrix.

## Boundaries

Static string hits are **hints**. Registry names are **metadata**.
Neither certifies feature completeness, redistribution terms, runtime thread
safety or industrial reading accuracy. Do not merge unverified restored
wrappers into the public stable API.

Current inventory and native registry outputs are published as GitHub Actions
artifacts in the Draft PR. `main` is unchanged.

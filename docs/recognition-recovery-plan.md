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
- A trial call to the native `HLILogicalName` registry using a range of
  unverified operator indices caused a Windows `AccessViolationException`.
  The experimental runtime probe and its CI were **removed**. Do not enumerate
  unknown indices until the native vendor contract, valid index range and ABI
  are verified. The failure does not establish any fault in barcode/OCR itself.
- The presence of a string or native logical name **does not establish** the
  correct argument slots, memory ownership, native license rights or reliable
  recognition with customer images. Missing names do not prove absence.
- All artifacts must record the exact managed commit and native DLL hash.

## Recovery decision sequence

1. Confirm the valid native operator-name query contract and index bounds
   from an authoritative ABI description **before any runtime introspection**.
   Use the static inventory for preliminary hints only.
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

Only the static inventory JSON/Markdown is published as GitHub Actions
artifacts. The experimental registry output was not produced because the
unsafe range scan crashed, and its workflow was removed. `main` is unchanged.

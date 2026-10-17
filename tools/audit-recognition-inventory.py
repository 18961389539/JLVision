#!/usr/bin/env python3
"""Static recognition capability inventory. Never executes JLVisionCore.dll.

Output categories:
 - managed API: authoritative for the scanned C# surface only;
 - removal script: evidence of a scripted removal RULE, not commit history;
 - native string: weak hint only; absent strings DO NOT prove absent native op.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

FAMILIES = {
    "barcode_1d": {
        "classes": ["JlBarCode"],
        "methods": ["CreateBarCodeModel", "FindBarCode", "GetBarCodeResult", "ClearBarCodeModel"],
        "binary_terms": ["bar_code", "barcode", "FindBarCode", "CreateBarCodeModel"],
    },
    "datacode_2d": {
        "classes": ["JlDataCode2D"],
        "methods": ["CreateDataCode2dModel", "FindDataCode2d", "GetDataCode2dResults", "ClearDataCode2dModel"],
        "binary_terms": ["data_code_2d", "datacode2d", "FindDataCode2d", "CreateDataCode2dModel"],
    },
    "ocr": {
        "classes": ["JlOCRBox", "JlOCRCnn", "JlOCRKnn", "JlOCRMlp", "JlOCRSvm", "JlTextModel"],
        "methods": ["DoOcrSingleClassMlp", "DoOcrWordMlp", "DoOcrSingleClassCnn",
                    "DoOcrWordCnn", "ReadOcrClassMlp", "FindText"],
        "binary_terms": ["do_ocr", "ocr_class", "OCR", "FindText"],
    },
    "ocv": {
        "classes": ["JlOCV"],
        "methods": ["ReadOcv", "WriteOcv", "SerializeOcv", "DeserializeOcv", "CloseOcv",
                    "CreateOcv", "DoOcvSimple", "DoOcvProj"],
        "binary_terms": ["ocv", "DoOcvSimple", "CreateOcv"],
    },
    "deep_counting": {
        "classes": ["JlDlModelCounting"],
        "methods": ["CreateDeepCountingModel", "ApplyDeepCountingModel",
                    "PrepareDeepCountingModel", "ReadDeepCountingModel"],
        "binary_terms": ["deep_counting", "DeepCounting"],
    },
}

def sha256(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for buf in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(buf)
    return h.hexdigest()

def identifiers(text):
    # Only signature and declaration lines; comments/usage examples are not counted.
    methods = set(re.findall(
        r"^\s*public\s+(?:(?:static|new|virtual|override|unsafe|sealed)\s+)*"
        r"(?:[\w<>\[\]?,.]+\s+)+([A-Za-z]\w*)\s*\(", text, flags=re.M))
    classes = set(re.findall(
        r"^\s*public\s+(?:(?:sealed|static|partial|abstract)\s+)*class\s+(Jl\w+)\b",
        text, flags=re.M))
    return methods, classes

def audit(root, native_path):
    runtime = root / "JLVisionLib.Runtime"
    if not runtime.is_dir():
        raise ValueError("Cannot locate JLVisionLib.Runtime under repository root")
    source_files = sorted(runtime.glob("*.cs"))
    if not source_files:
        raise ValueError("No C# runtime source files found")
    methods, classes = set(), set()
    for path in source_files:
        src = path.read_text(encoding="utf-8-sig")
        m, c = identifiers(src)
        methods.update(m)
        classes.update(c)
    # Removal scripts are hints; they do not prove any deletion happened in Git history.
    scripts = {}
    for rel in ("tools/remove-misc-modules.ps1", "tools/remove-ocr-3d-modules.ps1"):
        path = root / rel
        scripts[rel] = path.read_text(encoding="utf-8-sig") if path.is_file() else ""
    native_bytes = None
    native_hash = None
    native_size = None
    if native_path is not None:
        native_path = native_path.resolve()
        if not native_path.is_file():
            raise ValueError("Native DLL file not found")
        native_size = native_path.stat().st_size
        native_hash = sha256(native_path)
        native_bytes = native_path.read_bytes()
    families = {}
    for name, spec in FAMILIES.items():
        matched_methods = [m for m in spec["methods"] if m in methods]
        matched_classes = [c for c in spec["classes"] if c in classes]
        script_refs = [p for p, body in scripts.items()
                       if any(re.search(r"\b" + re.escape(c) + r"\b", body) for c in spec["classes"])]
        hints = {}
        if native_bytes is not None:
            # Exact byte-sequence presence is ONLY a static string hint, not an operator test.
            for term in spec["binary_terms"]:
                raw = term.encode("ascii")
                hints[term] = {
                    "ascii": raw.lower() in native_bytes.lower(),
                    "utf16le": term.encode("utf-16le") in native_bytes,
                }
        families[name] = {
            "managedClassNames": matched_classes,
            "managedMethodNames": matched_methods,
            "candidateMethodNamesNotFound": [m for m in spec["methods"] if m not in methods],
            "removalRuleScriptReferences": script_refs,
            "nativeStringHints": hints,
            "fullWorkflowVerified": False,
        }
    return {
        "schemaVersion": 1,
        "scope": "C# public declaration scan + removal-rule inspection + native static string hints",
        "managedSourceFiles": len(source_files),
        "native": {"inspected": native_bytes is not None, "sizeBytes": native_size,
                   "sha256": native_hash,
                   "warning": "A DLL string hit is not proof an operator can execute; lack of a hit is not proof of absence."},
        "families": families,
        "requiresRuntimeValidation": True,
        "modifiesRuntime": False,
    }

def markdown(report):
    lines = ["# JLVision Recognition API Inventory", "",
             "This is static evidence, NOT a native functional test.", "",
             f"Managed C# files scanned: {report['managedSourceFiles']}",
             f"Native DLL inspected: {report['native']['inspected']}", "",
             "| Family | Managed classes | Managed methods | Removal rules | Native hints |",
             "|---|---|---|---|---|"]
    for family, record in report["families"].items():
        positive = [k for k, v in record["nativeStringHints"].items() if any(v.values())]
        lines.append(f"| {family} | {', '.join(record['managedClassNames']) or 'none'} | "
                     f"{', '.join(record['managedMethodNames']) or 'none'} | "
                     f"{len(record['removalRuleScriptReferences'])} | "
                     f"{', '.join(positive) or 'none/uninspected'} |")
    lines += ["", "**Interpretation:** missing managed wrapper does not mean the native DLL lacks the algorithm.",
              "Native string presence/absence is inconclusive. Check exact native operator ID, ABI and real samples before restoring any wrapper.",
              "Removal-rule scripts do not establish a historical Git deletion.", ""]
    return "\n".join(lines)

def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path("."))
    parser.add_argument("--native", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--markdown", type=Path)
    args = parser.parse_args(argv)
    report = audit(args.root.resolve(), args.native)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if args.markdown:
        args.markdown.parent.mkdir(parents=True, exist_ok=True)
        args.markdown.write_text(markdown(report), encoding="utf-8")
    print(f"Recognition scan: {report['managedSourceFiles']} C# files; DLL static scan: {report['native']['inspected']}")
    for name, f in report["families"].items():
        positives = [k for k, v in f["nativeStringHints"].items() if any(v.values())]
        print(f"  {name}: managed={f['managedMethodNames']}; static DLL hints={positives}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())

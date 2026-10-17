#!/usr/bin/env python3
"""Offline field-acceptance evidence pack validator.

A complete pack is READY_FOR_HUMAN_REVIEW, never an automatic field certificate.
The declared provenance and identities remain claims needing independent checking.
"""
import argparse
import hashlib
import json
import math
import re
import sys
from datetime import datetime
from pathlib import Path

REQUIRED_FILES = ("calibrationAudit", "datasetBenchmark", "rawMetrology",
                  "robotPickTrials", "soakTestLog")
IDENTITY = ("stationId", "partNumber", "calibrationId", "cameraConfigId",
            "softwareCommit", "metrologyMethod", "metrologyInstrumentId",
            "metrologyCertificateId", "recordedUtc", "engineerId")
CRITERIA = ("minPositiveFrames", "minNegativeFrames", "maxPositionErrorMm",
            "maxAngleErrorDeg", "maxP95MatchMs", "maxMissRate",
            "maxFalsePositiveRate", "maxRepeatabilityStdDevMm",
            "maxCalibrationFitErrorMm", "maxCalibrationHoldoutErrorMm",
            "maxCalibrationHoldoutRmsMm", "minFitCalibrationGridCells",
            "minHoldoutCalibrationGridCells")


def value(record, name):
    if not isinstance(record, dict):
        return None
    return record.get(name, record.get(name[0].upper() + name[1:]))


def json_file(path):
    return json.loads(path.read_text(encoding="utf-8"),
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError("Nonfinite JSON constant: " + x)))


def contained_file(root, relative):
    if not isinstance(relative, str) or not relative or Path(relative).is_absolute() or "\\" in relative:
        raise ValueError("Expected a relative local file path")
    candidate = (root / relative).resolve()
    if not candidate.is_relative_to(root):
        raise ValueError("File reference escapes evidence directory")
    if not candidate.is_file():
        raise ValueError("Referenced evidence file not found")
    if candidate.stat().st_size == 0:
        raise ValueError("Referenced evidence file is empty")
    return candidate


def digest(path):
    hash_value = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            hash_value.update(chunk)
    return hash_value.hexdigest()


def utc_string(text):
    if not isinstance(text, str) or not text.endswith("Z"):
        return False
    try:
        datetime.fromisoformat(text.replace("Z", "+00:00"))
        return True
    except ValueError:
        return False


def evaluate(pack, root):
    errors = []
    inventory = []
    root = root.resolve()
    if not isinstance(pack, dict) or type(pack.get("schemaVersion")) is not int or pack["schemaVersion"] != 1:
        return {"status": "REVIEW_REQUIRED", "issues": ["Unsupported evidence schema"],
                "files": [], "scope": "Evidence completeness only; human approval required"}
    for name in IDENTITY:
        field = pack.get(name)
        if not isinstance(field, str) or not field.strip():
            errors.append("Missing identity: " + name)
    if not utc_string(pack.get("recordedUtc")):
        errors.append("recordedUtc must be an explicit UTC ISO-8601 timestamp")
    if pack.get("sourceType") != "field":
        errors.append("Source type is not field measurement")
    if any("SYNTHETIC" in str(pack.get(k, "")).upper() for k in IDENTITY):
        errors.append("Synthetic demonstration identifiers are not field evidence")
    refs = pack.get("evidenceFiles")
    if not isinstance(refs, dict):
        refs = {}
        errors.append("Missing evidenceFiles mapping")
    verified = {}
    for name in REQUIRED_FILES:
        record = refs.get(name)
        if not isinstance(record, dict):
            errors.append("Missing evidence file: " + name)
            continue
        try:
            expected = record.get("sha256")
            if not isinstance(expected, str) or not re.fullmatch(r"[0-9a-f]{64}", expected):
                raise ValueError("Expected lowercase 64-character SHA256")
            path = contained_file(root, record.get("path"))
            actual = digest(path)
            if actual != expected:
                raise ValueError("SHA256 mismatch")
            verified[name] = path
            inventory.append({"kind": name, "path": record["path"],
                              "sha256": actual, "bytes": path.stat().st_size})
        except (ValueError, OSError) as exc:
            errors.append(name + ": " + str(exc))
    if "calibrationAudit" in verified:
        try:
            audit = json_file(verified["calibrationAudit"])
            result = audit.get("result")
            if value(result, "calibrationId") != pack.get("calibrationId"):
                errors.append("Calibration audit ID does not match evidence pack")
            if value(result, "passed") is not True:
                errors.append("Calibration audit failed or lacks explicit pass")
            fit = value(result, "fit")
            holdout = value(result, "holdout")
            if not isinstance(value(fit, "count"), int) or value(fit, "count") < 4:
                errors.append("Insufficient calibration fit points")
            if not isinstance(value(holdout, "count"), int) or value(holdout, "count") < 3:
                errors.append("Insufficient independent holdout points")
            history = audit.get("history")
            if not isinstance(history, dict) or value(history, "regressionDetected") is not False:
                errors.append("Historical calibration regression or history is unavailable")
            if isinstance(history, dict):
                if value(history, "lastId") != pack.get("calibrationId"):
                    errors.append("History current version ID mismatch")
                versions = value(history, "versions")
                steps = value(history, "steps")
                if (type(versions) is not int or versions < 2 or
                        not isinstance(steps, list) or len(steps) != versions - 1):
                    errors.append("Calibration history version steps are missing or incomplete")
                else:
                    for index, step in enumerate(steps):
                        if (not isinstance(step, dict) or
                                not isinstance(value(step, "regressionDetected"), bool) or
                                not isinstance(value(step, "baselineId"), str) or
                                not isinstance(value(step, "currentId"), str)):
                            errors.append("Invalid history transition " + str(index + 1))
                            break
        except (ValueError, OSError, AttributeError, TypeError) as exc:
            errors.append("Invalid calibration audit JSON: " + str(exc))
    if "datasetBenchmark" in verified:
        try:
            ds = json_file(verified["datasetBenchmark"])
            if ds.get("syntheticFixture") is not False:
                errors.append("Dataset uses synthetic or unverified fixture")
            if ds.get("allAlgorithmsPassed") is not True:
                errors.append("Dataset algorithm acceptance failed")
            if value(ds.get("calibrationAudit"), "calibrationId") != pack.get("calibrationId"):
                errors.append("Dataset calibration ID mismatch")
            if value(ds.get("calibrationAudit"), "passed") is not True:
                errors.append("Dataset embedded calibration audit is not explicitly passed")
            acceptance = ds.get("acceptance")
            for key in CRITERIA:
                check = value(acceptance, key)
                if not isinstance(check, (float, int)) or isinstance(check, bool) or not math.isfinite(check) or check < (1 if key.startswith("min") else 0):
                    errors.append("Missing or invalid dataset acceptance criterion: " + key)
            results = ds.get("summary")
            if not isinstance(results, list) or not results:
                errors.append("Dataset algorithm summary is empty")
            else:
                for alg in results:
                    if not isinstance(alg, dict) or alg.get("acceptancePassed") is not True:
                        errors.append("Algorithm acceptance missing or failed")
                        continue
                    if not isinstance(alg.get("truePositive"), int) or alg["truePositive"] <= 0:
                        errors.append("Missing verified positive frames")
                    if not isinstance(alg.get("trueNegative"), int) or alg["trueNegative"] <= 0:
                        errors.append("Missing verified negative frames")
                    if not isinstance(alg.get("annotatedWorldMatches"), int) or alg["annotatedWorldMatches"] <= 0:
                        errors.append("Missing millimeter world annotations")
            if not isinstance(ds.get("datasetFrameCount"), int) or ds["datasetFrameCount"] <= 0:
                errors.append("Missing dataset frame count")
        except (ValueError, OSError, AttributeError, TypeError) as exc:
            errors.append("Invalid dataset JSON: " + str(exc))
    issues = sorted(set(errors))
    return {"schemaVersion": 1,
            "status": "READY_FOR_HUMAN_REVIEW" if not issues else "REVIEW_REQUIRED",
            "scope": "Evidence file integrity and declared software quality gates only",
            "independentSourceVerified": False,
            "robotAccuracyCertified": False,
            "humanApprovalRequired": True,
            "stationId": pack.get("stationId"),
            "calibrationId": pack.get("calibrationId"),
            "files": inventory,
            "issues": issues}


def markdown_report(report):
    lines = ["# JLVision Field Evidence Review", "",
             "**Status:** " + report["status"], "",
             "**This is not a field acceptance certificate.** Hashes prove local file consistency only, not independent provenance or robot accuracy.", "",
             "Station: " + str(report.get("stationId") or "(missing)"),
             "Calibration: " + str(report.get("calibrationId") or "(missing)"), "",
             "## Outstanding issues", ""]
    lines += ["- " + issue.replace("\n", " ") for issue in report["issues"]] or ["- None for the machine-checked scope; human metrology and sign-off still required"]
    lines += ["", "## Evidence inventory", "",
              "| Type | SHA256 | Size (bytes) |", "|---|---|---:|"]
    for item in report["files"]:
        lines.append("| " + item["kind"] + " | " + item["sha256"] + " | " + str(item["bytes"]) + " |")
    lines += ["", "Engineer sign-off: __________  Quality sign-off: __________", ""]
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--markdown", type=Path)
    args = parser.parse_args(argv)
    pack = json_file(args.manifest)
    result = evaluate(pack, args.manifest.resolve().parent)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if args.markdown:
        args.markdown.parent.mkdir(parents=True, exist_ok=True)
        args.markdown.write_text(markdown_report(result), encoding="utf-8")
    print(result["status"], "-", len(result["issues"]), "issue(s)")
    return 0 if not result["issues"] else 2


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, json.JSONDecodeError) as ex:
        print("Evidence input error:", ex, file=sys.stderr)
        sys.exit(1)

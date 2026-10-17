import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).with_name("field-evidence-audit.py")
spec = importlib.util.spec_from_file_location("field_evidence_audit", SCRIPT)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class FieldEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.pack = {
            "schemaVersion": 1,
            "sourceType": "field",
            "stationId": "CELL-1", "partNumber": "PART-A",
            "calibrationId": "cal-v2", "cameraConfigId": "cam-v1",
            "softwareCommit": "a" * 40,
            "metrologyMethod": "Independent reference measurement",
            "metrologyInstrumentId": "gauge-1",
            "metrologyCertificateId": "cert-1",
            "recordedUtc": "2026-10-17T03:00:00Z",
            "engineerId": "reviewer-1",
            "evidenceFiles": {},
        }
        data = {
            "result": {"CalibrationId": "cal-v2", "Passed": True,
                       "Fit": {"Count": 5}, "Holdout": {"Count": 3}},
            "history": {"LastId": "cal-v2", "RegressionDetected": False,
                        "Versions": 2,
                        "Steps": [{"BaselineId": "cal-v1", "CurrentId": "cal-v2",
                                   "RegressionDetected": False}]},
        }
        ds = {
            "syntheticFixture": False, "allAlgorithmsPassed": True,
            "datasetFrameCount": 6,
            "calibrationAudit": {"CalibrationId": "cal-v2", "Passed": True},
            "acceptance": {key: (1 if key.startswith("min") else 0.1) for key in audit.CRITERIA},
            "summary": [{"algorithm": "shape", "acceptancePassed": True,
                         "truePositive": 4, "trueNegative": 2, "annotatedWorldMatches": 4}],
        }
        for key in audit.REQUIRED_FILES:
            contents = (json.dumps(data if key == "calibrationAudit" else ds).encode()
                        if key in ("calibrationAudit", "datasetBenchmark") else b"FAKE TEST DATA")
            path = self.root / (key + ".json" if key in ("calibrationAudit", "datasetBenchmark") else key + ".txt")
            path.write_bytes(contents)
            self.pack["evidenceFiles"][key] = {
                "path": path.name,
                "sha256": hashlib.sha256(contents).hexdigest(),
            }

    def test_complete_pack_only_ready_for_human_review(self):
        result = audit.evaluate(self.pack, self.root)
        self.assertEqual("READY_FOR_HUMAN_REVIEW", result["status"])
        self.assertFalse(result["independentSourceVerified"])
        self.assertFalse(result["robotAccuracyCertified"])
        self.assertTrue(result["humanApprovalRequired"])
        self.assertEqual(5, len(result["files"]))

    def test_missing_file_fails_closed(self):
        self.pack["evidenceFiles"].pop("robotPickTrials")
        result = audit.evaluate(self.pack, self.root)
        self.assertEqual("REVIEW_REQUIRED", result["status"])
        self.assertIn("Missing evidence file: robotPickTrials", result["issues"])

    def test_tampered_hash_fails_closed(self):
        self.pack["evidenceFiles"]["rawMetrology"]["sha256"] = "0" * 64
        result = audit.evaluate(self.pack, self.root)
        self.assertTrue(any("SHA256 mismatch" in issue for issue in result["issues"]))

    def test_traversal_is_rejected(self):
        self.pack["evidenceFiles"]["robotPickTrials"]["path"] = "../outside.txt"
        self.assertTrue(any("robotPickTrials:" in x for x in
                            audit.evaluate(self.pack, self.root)["issues"]))

    def test_synthetic_identity_is_not_field_evidence(self):
        self.pack["stationId"] = "SYNTHETIC-EXAMPLE"
        self.assertEqual("REVIEW_REQUIRED", audit.evaluate(self.pack, self.root)["status"])

    def test_regression_is_not_accepted(self):
        path = self.root / "calibrationAudit.json"
        payload = json.loads(path.read_text())
        payload["history"]["RegressionDetected"] = True
        contents = json.dumps(payload).encode()
        path.write_bytes(contents)
        self.pack["evidenceFiles"]["calibrationAudit"]["sha256"] = hashlib.sha256(contents).hexdigest()
        issues = audit.evaluate(self.pack, self.root)["issues"]
        self.assertIn("Historical calibration regression or history is unavailable", issues)

    def test_dataset_synthetic_flag_is_not_accepted(self):
        path = self.root / "datasetBenchmark.json"
        payload = json.loads(path.read_text())
        payload["syntheticFixture"] = True
        contents = json.dumps(payload).encode()
        path.write_bytes(contents)
        self.pack["evidenceFiles"]["datasetBenchmark"]["sha256"] = hashlib.sha256(contents).hexdigest()
        issues = audit.evaluate(self.pack, self.root)["issues"]
        self.assertIn("Dataset uses synthetic or unverified fixture", issues)

    def test_incomplete_history_rejected(self):
        path = self.root / "calibrationAudit.json"
        record = json.loads(path.read_text())
        record["history"]["Steps"] = []
        data = json.dumps(record).encode()
        path.write_bytes(data)
        self.pack["evidenceFiles"]["calibrationAudit"]["sha256"] = hashlib.sha256(data).hexdigest()
        self.assertIn("Calibration history version steps are missing or incomplete",
                      audit.evaluate(self.pack, self.root)["issues"])

    def test_markdown_does_not_claim_certification(self):
        result = audit.evaluate(self.pack, self.root)
        rendered = audit.markdown_report(result)
        self.assertIn("not a field acceptance certificate", rendered)
        self.assertIn("Quality sign-off", rendered)

    def test_cli_exit_code_for_incomplete_pack(self):
        path = self.root / "manifest.json"
        path.write_text(json.dumps({"schemaVersion": 1, "sourceType": "synthetic"}))
        result_path = self.root / "result.json"
        exit_code = audit.main(["--manifest", str(path), "--output", str(result_path)])
        self.assertEqual(2, exit_code)
        self.assertEqual("REVIEW_REQUIRED", json.loads(result_path.read_text())["status"])


if __name__ == "__main__":
    unittest.main()

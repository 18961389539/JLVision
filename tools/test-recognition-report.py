import copy
import importlib.util
from pathlib import Path
import math
import unittest

SCRIPT = Path(__file__).with_name("recognition-report.py")
spec = importlib.util.spec_from_file_location("recognition_report", SCRIPT)
renderer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(renderer)


def ci(k, n):
    if n == 0:
        return {"events": 0, "trials": 0, "estimate": None, "lower95": None, "upper95": None}
    z = 1.959963984540054
    p = k / n
    denom = 1 + z * z / n
    center = (p + z * z / (2 * n)) / denom
    half = (z / denom) * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return {"events": k, "trials": n, "estimate": p, "lower95": max(0, center-half),
            "upper95": min(1, center+half)}


def metrics(p=2, n=2, tp=2, fp=0, misread=0):
    fn = p-tp
    return {
        "total": p+n, "positive": p, "negative": n,
        "truePositive": tp, "falseNegative": fn,
        "trueNegative": n-fp, "falsePositive": fp,
        "misread": misread, "processingErrors": 0,
        "missRate": fn/p if p else 1,
        "misreadRate": misread/p if p else 1,
        "falsePositiveRate": fp/n if n else 1,
        "p95Ms": 23.5,
        "uncertainty": {"correctRead": ci(tp,p), "miss": ci(fn,p),
                        "misread": ci(misread,p), "falsePositive": ci(fp,n)}
    }


def report():
    m = metrics()
    return {
        "schemaVersion": 1,
        "datasetId": "SYNTHETIC-DEMO-<test>",
        "sourceType": "synthetic",
        "declaredSoftwareCommit": "a"*40,
        "manifestSha256": "b"*64,
        "verdict": "SYNTHETIC_SOFTWARE_GATES_PASSED",
        "realFieldAccuracyCertified": False,
        "acceptance": {
            "maxMissRate": 0, "maxMisreadRate": 0,
            "maxFalsePositiveRate": 0, "maxP95Ms": 100,
            "minPositivePerKind": 1, "minNegativePerKind": 1,
            "minPositivePerCondition": 0, "minNegativePerCondition": 0,
            "requireWilsonUpper95": False,
        },
        "overall": m,
        "byKind": {"barcode1d": m},
        "byCondition": {"rotated<script>alert(1)</script>": m},
        "byKindCondition": {"barcode1d|rotated": m},
        "reasons": []
    }


class RecognitionHtmlTests(unittest.TestCase):
    def test_contains_full_95_interval_and_all_group_tables(self):
        content = renderer.render(report())
        self.assertIn("Wilson 95% upper bound", content)
        self.assertIn("65.8%", content)
        self.assertIn("By algorithm × condition", content)
        self.assertIn("P95 processing", content)
        self.assertIn("not independently verified", content)
        self.assertIn("SYNTHETIC DEMONSTRATION", content)

    def test_all_untrusted_ids_and_scenario_labels_escaped(self):
        content = renderer.render(report())
        self.assertNotIn("<test>", content)
        self.assertNotIn("<script>", content)
        self.assertIn("&lt;test&gt;", content)
        self.assertIn("&lt;script&gt;", content)

    def test_invalid_matrix_rejected(self):
        p = report()
        p["overall"]["truePositive"] = 3
        with self.assertRaises(ValueError):
            renderer.render(p)

    def test_impossible_intervals_rejected(self):
        p = report()
        p["overall"]["uncertainty"]["miss"]["upper95"] = -1
        with self.assertRaises(ValueError):
            renderer.render(p)

    def test_missing_n_is_explicitly_na(self):
        p = report()
        m = metrics(p=2, n=0)
        p["overall"] = m
        p["byKind"] = {"barcode1d": m}
        p["byCondition"] = {"normal": m}
        p["byKindCondition"] = {"barcode1d|normal": m}
        content = renderer.render(p)
        self.assertIn("N/A (n=0)", content)

    def test_failing_gate_reasons_visible_and_escaped(self):
        p = report()
        p["verdict"] = "REVIEW_REQUIRED"
        p["reasons"] = ['Low light <img src=x onerror="alert(1)"> failed']
        content = renderer.render(p)
        self.assertIn("REVIEW REQUIRED", content)
        self.assertIn("&lt;img", content)
        self.assertNotIn("<img src=x", content)

    def test_field_verdict_does_not_become_certification(self):
        p = report()
        p["sourceType"] = "field"
        p["verdict"] = "SOFTWARE_GATES_PASSED_HUMAN_REVIEW_REQUIRED"
        content = renderer.render(p)
        self.assertIn("DECLARED FIELD DATA", content)
        self.assertIn("Real field accuracy certified:</b> NO", content)
        p["realFieldAccuracyCertified"] = True
        with self.assertRaises(ValueError):
            renderer.render(p)

    def test_synthetic_cannot_claim_field_verdict(self):
        p = report()
        p["verdict"] = "SOFTWARE_GATES_PASSED_HUMAN_REVIEW_REQUIRED"
        with self.assertRaises(ValueError):
            renderer.render(p)

    def test_missing_wilson_policy_rejected(self):
        p = report()
        del p["acceptance"]["requireWilsonUpper95"]
        with self.assertRaises(ValueError):
            renderer.render(p)

    def test_contradictory_success_with_failures_rejected(self):
        p = report()
        p["reasons"] = ["No"]
        with self.assertRaises(ValueError):
            renderer.render(p)


if __name__ == "__main__":
    unittest.main()

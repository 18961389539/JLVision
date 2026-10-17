import importlib.util
import pathlib
import tempfile
import unittest

SCRIPT = pathlib.Path(__file__).with_name("calibration-history-report.py")
spec = importlib.util.spec_from_file_location("calibration_history_report", SCRIPT)
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


class CalibrationHistoryReportTests(unittest.TestCase):
    def test_render_valid_history(self):
        html = report.render({
            "result": {"CalibrationId": "DEMO"},
            "history": {
                "Versions": 3,
                "RegressionDetected": True,
                "NetHoldoutMaxChangeMm": 0.01,
                "NetHoldoutRmsChangeMm": 0.003,
            }
        })
        self.assertIn("REGRESSION", html)
        self.assertIn("+0.01000 mm", html)
        self.assertIn("DEMO", html)

    def test_missing_history_rejected(self):
        with self.assertRaises(ValueError):
            report.render({})

    def test_missing_verdict_rejected(self):
        with self.assertRaises(ValueError):
            report.render({"history": {"Versions": 2}})

    def test_html_escape(self):
        html = report.render({"result": {"CalibrationId": "<script>"},
                              "history": {"Versions": 2, "RegressionDetected": False}})
        self.assertIn("&lt;script&gt;", html)
        self.assertNotIn("<script>", html)

    def test_step_chart_and_streak(self):
        rendered = report.render({
            "history": {"versions": 3, "regressionDetected": True,
                        "netHoldoutMaxChangeMm": 0.03,
                        "netHoldoutRmsChangeMm": 0.012,
                        "steps": [
                            {"baselineId": "v1", "currentId": "v2",
                             "holdoutMaxChangeMm": 0.01, "holdoutRmsChangeMm": 0.004,
                             "regressionDetected": False},
                            {"baselineId": "v2", "currentId": "v3",
                             "holdoutMaxChangeMm": 0.02, "holdoutRmsChangeMm": 0.008,
                             "regressionDetected": True}]}})
        self.assertIn("<svg", rendered)
        self.assertIn("2 transitions", rendered)
        self.assertIn("v2 → v3", rendered)

    def test_wrong_step_count_rejected(self):
        with self.assertRaises(ValueError):
            report.render({"history": {"versions": 3, "regressionDetected": False,
                                       "steps": []}})

    def test_inconsistent_cumulative_delta_rejected(self):
        with self.assertRaises(ValueError):
            report.render({"history": {"versions": 2, "regressionDetected": False,
                "netHoldoutMaxChangeMm": 0.2, "netHoldoutRmsChangeMm": 0,
                "steps": [{"holdoutMaxChangeMm": 0.1, "holdoutRmsChangeMm": 0,
                           "regressionDetected": False}]}})

    def test_step_label_escaped(self):
        rendered = report.render({"history": {"versions": 2,
            "regressionDetected": False, "netHoldoutMaxChangeMm": 0,
            "netHoldoutRmsChangeMm": 0, "steps": [{
                "baselineId": "<script>", "currentId": "v2",
                "holdoutMaxChangeMm": 0, "holdoutRmsChangeMm": 0,
                "regressionDetected": False}]}})
        self.assertNotIn("<script>", rendered)
        self.assertIn("&lt;script&gt;", rendered)

    def test_consecutive_gate_triggered(self):
        sample = {"history": {"versions": 3, "regressionDetected": False,
            "netHoldoutMaxChangeMm": 0.03, "netHoldoutRmsChangeMm": 0.02,
            "steps": [
                {"holdoutMaxChangeMm": 0.01, "holdoutRmsChangeMm": 0.01, "regressionDetected": False},
                {"holdoutMaxChangeMm": 0.02, "holdoutRmsChangeMm": 0.01, "regressionDetected": False}]},
            "maxConsecutiveDegradingSteps": 2}
        self.assertIn("CONTINUOUS DEGRADATION LIMIT REACHED", report.render(sample))

    def test_missing_steps_with_gate_rejected(self):
        with self.assertRaises(ValueError):
            report.render({"history": {"versions": 2, "regressionDetected": False},
                           "maxConsecutiveDegradingSteps": 2})

    def test_invalid_gate_rejected(self):
        with self.assertRaises(ValueError):
            report.render({"history": {"versions": 2, "regressionDetected": False},
                           "maxConsecutiveDegradingSteps": 0})

    def test_audit_summary_missing_evidence_fails_closed(self):
        issues = report.gate_status({"history": {"versions": 2, "regressionDetected": False}})
        self.assertIn("Current calibration audit pass/fail evidence missing", issues)

    def test_audit_summary_can_pass_with_explicit_evidence(self):
        issues = report.gate_status({"result": {"passed": True},
            "history": {"versions": 2, "regressionDetected": False}})
        self.assertEqual([], issues)

    def test_audit_summary_regression_blocks_pass(self):
        issues = report.gate_status({"result": {"passed": True},
            "history": {"versions": 2, "regressionDetected": True}})
        self.assertIn("Historical calibration regression detected", issues)

    def test_nonfinite_is_not_passed_through(self):
        self.assertEqual(report.fmt(float("nan")), "N/A")


if __name__ == "__main__":
    unittest.main()

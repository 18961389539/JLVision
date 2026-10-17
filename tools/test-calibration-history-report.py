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

    def test_nonfinite_is_not_passed_through(self):
        self.assertEqual(report.fmt(float("nan")), "N/A")


if __name__ == "__main__":
    unittest.main()

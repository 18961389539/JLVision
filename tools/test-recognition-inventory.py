import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).with_name("audit-recognition-inventory.py")
spec = importlib.util.spec_from_file_location("audit_recognition_inventory", SCRIPT)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


class RecognitionInventoryTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / "JLVisionLib.Runtime").mkdir()
        (self.root / "tools").mkdir()
        (self.root / "JLVisionLib.Runtime" / "JlOperatorSet.cs").write_text(
            '/// FindBarCode(documented only)\n'
            'public static void ReadOcv(JlTuple name, out JlTuple handle) {}\n'
            'public static void ApplyDeepCountingModel(JlObject image) {}\n'
            'public class JlExample {}\n', encoding="utf-8")
        (self.root / "tools" / "remove-misc-modules.ps1").write_text(
            "$deleteFiles = @('JlBarCode.cs', 'JlDataCode2D.cs', 'JlOCV.cs')",
            encoding="utf-8")
        self.dll = self.root / "test.dll"
        self.dll.write_bytes(b"mock DLL says FindBarCode but provides no API")

    def test_comments_do_not_create_a_fake_api(self):
        methods, classes = audit.identifiers(
            "/// public static void FindBarCode()\n"
            "public static void ReadOcv(JlTuple file) {}\n")
        self.assertNotIn("FindBarCode", methods)
        self.assertIn("ReadOcv", methods)

    def test_managed_and_native_are_separate_evidence(self):
        report = audit.audit(self.root, self.dll)
        barcode = report["families"]["barcode_1d"]
        self.assertEqual([], barcode["managedMethodNames"])
        self.assertTrue(barcode["nativeStringHints"]["FindBarCode"]["ascii"])
        self.assertEqual(1, len(barcode["removalRuleScriptReferences"]))
        self.assertFalse(barcode["fullWorkflowVerified"])
        self.assertTrue(report["requiresRuntimeValidation"])

    def test_ocv_partial_methods_detected(self):
        report = audit.audit(self.root, None)
        self.assertIn("ReadOcv", report["families"]["ocv"]["managedMethodNames"])
        self.assertEqual({}, report["families"]["ocv"]["nativeStringHints"])

    def test_unicode_string_hint_does_not_imply_working_operator(self):
        self.dll.write_bytes("CreateBarCodeModel".encode("utf-16le"))
        report = audit.audit(self.root, self.dll)
        self.assertTrue(report["families"]["barcode_1d"]
                        ["nativeStringHints"]["CreateBarCodeModel"]["utf16le"])
        self.assertFalse(report["families"]["barcode_1d"]["fullWorkflowVerified"])

    def test_missing_dll_is_input_error(self):
        with self.assertRaises(ValueError):
            audit.audit(self.root, self.root / "missing.dll")

    def test_output_report_is_valid(self):
        report = audit.audit(self.root, self.dll)
        rendered = audit.markdown(report)
        self.assertIn("not mean the native DLL lacks", rendered)
        self.assertIn("Removal-rule scripts", rendered)
        p = self.root / "inventory.json"
        p.write_text(json.dumps(report), encoding="utf-8")
        self.assertEqual(1, json.loads(p.read_text())["schemaVersion"])


if __name__ == "__main__":
    unittest.main()

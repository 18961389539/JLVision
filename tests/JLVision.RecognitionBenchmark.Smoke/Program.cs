using System.Diagnostics;
using System.Text.Json;
using JLVision.RecognitionBenchmark;
using ZXing;
using ZXing.Common;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception("Recognition acceptance smoke failed: " + message);
}
static byte[] MakeSymbol(BarcodeFormat format, string text, int width, int height)
{
    var writer = new BarcodeWriterPixelData
    {
        Format = format,
        Options = new EncodingOptions { Width = width, Height = height, Margin = 16, PureBarcode = true }
    };
    var bitmap = writer.Write(text);
    Check(bitmap.Width == width && bitmap.Height == height, "unexpected synthetic symbol dimensions");
    byte[] gray = new byte[width * height];
    for (int i = 0; i < gray.Length; i++) gray[i] = bitmap.Pixels[4 * i];
    return gray;
}
static void WriteGrayBmp(string path, byte[] pixels, int width, int height)
{
    const int off = 14 + 40 + 1024;
    int pitch = (width + 3) & ~3;
    int size = pitch * height;
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write((byte)'B'); writer.Write((byte)'M');
    writer.Write(off + size);
    writer.Write((short)0); writer.Write((short)0);
    writer.Write(off); writer.Write(40); writer.Write(width); writer.Write(height);
    writer.Write((short)1); writer.Write((short)8);
    writer.Write(0); writer.Write(size);
    writer.Write(2835); writer.Write(2835); writer.Write(256); writer.Write(0);
    for (int i = 0; i < 256; i++)
    {
        writer.Write((byte)i); writer.Write((byte)i); writer.Write((byte)i); writer.Write((byte)0);
    }
    for (int row = height - 1; row >= 0; row--)
    {
        writer.Write(pixels, row * width, width);
        for (int p = width; p < pitch; p++) writer.Write((byte)0);
    }
}
static RecognitionFrame Frame(string id, string file, string kind, bool present,
    string? expectedText = null, string? expectedFormat = null) => new()
{
    Id = id, File = file, Kind = kind,
    Condition = present ? "normal" : "blank",
    ExpectedPresent = present, ExpectedText = expectedText, ExpectedFormat = expectedFormat
};
static RecognitionObservation Observ(RecognitionFrame f, bool detected, bool accepted, bool misread, double ms) =>
    new(f.Id, f.Kind, f.Condition, f.ExpectedPresent!.Value, detected, accepted, misread,
        ms, detected ? 1 : 0, new string('a', 64));

var root = Path.Combine(Path.GetTempPath(), "jlv-recognition-acceptance-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    WriteGrayBmp(Path.Combine(root, "code128.bmp"),
        MakeSymbol(BarcodeFormat.CODE_128, "JLV-128-81", 480, 120), 480, 120);
    WriteGrayBmp(Path.Combine(root, "qr.bmp"),
        MakeSymbol(BarcodeFormat.QR_CODE, "JLV-QR-81", 256, 256), 256, 256);
    WriteGrayBmp(Path.Combine(root, "datamatrix.bmp"),
        MakeSymbol(BarcodeFormat.DATA_MATRIX, "JLV-DM-81", 256, 256), 256, 256);
    WriteGrayBmp(Path.Combine(root, "blank.bmp"),
        Enumerable.Repeat((byte)255, 256 * 256).ToArray(), 256, 256);
    var dataset = new RecognitionDataset
    {
        SchemaVersion = 1,
        SourceType = "synthetic",
        DatasetId = "SYNTHETIC-DEMO-ONLY",
        SoftwareCommit = new string('a', 40),
        Acceptance = new RecognitionLimits
        {
            MinPositivePerKind = 1, MinNegativePerKind = 1,
            MinPositivePerCondition = 0, MinNegativePerCondition = 0,
            MaxMissRate = 0, MaxMisreadRate = 0,
            MaxFalsePositiveRate = 0, MaxP95Ms = 30000,
            RequiredKinds = new() { "barcode1d", "datacode2d" },
            RequiredConditions = new() { "normal", "blank" }
        },
        Frames = new()
        {
            Frame("1d-pos", "code128.bmp", "barcode1d", true, "JLV-128-81", "CODE_128"),
            Frame("1d-neg", "blank.bmp", "barcode1d", false),
            Frame("qr-pos", "qr.bmp", "datacode2d", true, "JLV-QR-81", "QR_CODE"),
            Frame("qr-neg", "blank.bmp", "datacode2d", false),
            Frame("dm-pos", "datamatrix.bmp", "datacode2d", true, "JLV-DM-81", "DATA_MATRIX"),
            Frame("dm-neg", "blank.bmp", "datacode2d", false)
        }
    };
    var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    string manifest = Path.Combine(root, "manifest.json");
    string output = Path.Combine(root, "report.json");
    File.WriteAllText(manifest, JsonSerializer.Serialize(dataset, json));
    RecognitionEvaluator.Validate(dataset);

    // Verify independent metric semantics without relying on a particular decoder.
    var baseObserv = dataset.Frames.Select((f, i) =>
        Observ(f, f.ExpectedPresent!.Value, f.ExpectedPresent.Value, false, i + 1)).ToList();
    var pass = RecognitionEvaluator.Evaluate(dataset, baseObserv, new string('0', 64));
    Check(pass.Verdict == "SYNTHETIC_SOFTWARE_GATES_PASSED", "correct synthetic verdict");
    Check(!pass.RealFieldAccuracyCertified, "no field certification");
    Check(pass.Overall.TruePositive == 3 && pass.Overall.TrueNegative == 3, "TP/TN");
    Check(Math.Abs(pass.Overall.P95Ms - 6) < 0.001, "nearest-rank P95");
    Check(pass.ByKindCondition.ContainsKey("barcode1d|normal") &&
          pass.ByKindCondition.ContainsKey("datacode2d|blank"), "algorithm-condition coverage");

    dataset.Frames[0].ExpectedFormat = "QR_CODE";
    bool rejectedFormat = false;
    try { RecognitionEvaluator.Validate(dataset); }
    catch (InvalidDataException) { rejectedFormat = true; }
    Check(rejectedFormat, "a QR label cannot be assigned to a 1D reader");
    dataset.Frames[0].ExpectedFormat = "CODE_128";

    var wrong = baseObserv.ToList();
    wrong[2] = Observ(dataset.Frames[2], true, false, true, 3);
    var wrongReport = RecognitionEvaluator.Evaluate(dataset, wrong, new string('0', 64));
    Check(wrongReport.Overall.Misread == 1 && wrongReport.Overall.FalseNegative == 1, "wrong content must be misread and missed");
    Check(wrongReport.Reasons.Any(r => r.Contains("misread rate")), "misread quality gate");

    var falseAlarm = baseObserv.ToList();
    falseAlarm[3] = Observ(dataset.Frames[3], true, false, false, 4);
    var falseAlarmReport = RecognitionEvaluator.Evaluate(dataset, falseAlarm, new string('0', 64));
    Check(falseAlarmReport.Overall.FalsePositive == 1, "false positive on negative image");
    Check(falseAlarmReport.Reasons.Any(r => r.Contains("false-positive rate")), "false-positive gate");

    var missing = baseObserv.Take(5).ToList();
    bool rejected = false;
    try { RecognitionEvaluator.Evaluate(dataset, missing, ""); }
    catch (InvalidDataException) { rejected = true; }
    Check(rejected, "partial dataset must not pass");

    dataset.Acceptance.MinPositivePerKind = 2;
    var underpowered = RecognitionEvaluator.Evaluate(dataset, baseObserv, "");
    Check(underpowered.Reasons.Any(r => r.Contains("insufficient")), "minimum population gate");
    dataset.Acceptance.MinPositivePerKind = 1;

    // Each algorithm must be represented in every REQUIRED condition.
    dataset.Frames[3].Condition = "normal";
    dataset.Frames[5].Condition = "normal";
    var missingConditionObservations = dataset.Frames.Select((f, i) =>
        Observ(f, f.ExpectedPresent!.Value, f.ExpectedPresent.Value, false, i + 1)).ToList();
    var missingCondition = RecognitionEvaluator.Evaluate(dataset, missingConditionObservations, "");
    Check(missingCondition.Reasons.Any(r =>
          r.Contains("Missing required algorithm-condition combination: datacode2d|blank")),
          "missing per-algorithm scenario must fail closed");
    dataset.Frames[3].Condition = "blank";
    dataset.Frames[5].Condition = "blank";

    // Actual JlImage -> ZXing barcode pipeline and JSON report, using synthetic BMPs.
    string project = Path.Combine(Directory.GetCurrentDirectory(), "tools",
        "JLVision.RecognitionBenchmark", "JLVision.RecognitionBenchmark.csproj");
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Directory.GetCurrentDirectory()
    };
    foreach (string arg in new[] { "run", "--no-build", "--project", project,
                  "--configuration", "Debug", "--", "--manifest", manifest, "--output", output })
        start.ArgumentList.Add(arg);
    using var process = Process.Start(start) ?? throw new Exception("Unable to spawn dataset benchmark");
    string stdout = process.StandardOutput.ReadToEnd();
    string stderr = process.StandardError.ReadToEnd();
    Check(process.WaitForExit(120000), "benchmark process timeout");
    Console.WriteLine(stdout);
    if (process.ExitCode != 0) Console.Error.WriteLine(stderr);
    Check(process.ExitCode == 0, "end-to-end recognition benchmark failed");
    using var report = JsonDocument.Parse(File.ReadAllText(output));
    var rootElement = report.RootElement;
    Check(rootElement.GetProperty("verdict").GetString() == "SYNTHETIC_SOFTWARE_GATES_PASSED",
        "end-to-end synthetic verdict");
    Check(rootElement.GetProperty("realFieldAccuracyCertified").GetBoolean() is false, "never certify synthetic data");
    Check(rootElement.GetProperty("overall").GetProperty("truePositive").GetInt32() == 3,
        "real decoder must read Code128, QR and DataMatrix");
    Check(rootElement.GetProperty("overall").GetProperty("trueNegative").GetInt32() == 3,
        "negative images must have no detections");
    Check(rootElement.GetProperty("byKindCondition").GetProperty("barcode1d|normal")
        .GetProperty("truePositive").GetInt32() == 1, "per-kind condition results");
    var artifactDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
    Directory.CreateDirectory(artifactDirectory);
    File.Copy(output, Path.Combine(artifactDirectory, "recognition-dataset.synthetic.json"), true);
    Console.WriteLine("PASS recognition metrics, failure gates, dataset execution and explicit synthetic labeling.");
}
finally
{
    Directory.Delete(root, true);
}

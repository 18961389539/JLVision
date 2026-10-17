using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using JLVisionLib;
using JLVisionLib.Recognition;
using JLVisionLib.Recognition.TesseractBackend;
using JLVision.RecognitionBenchmark;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Recognition benchmark input/runtime error: " + ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("JLVision native image decoding requires Windows x64.");

        string manifestPath = RequiredOption(args, "--manifest");
        string outputPath = RequiredOption(args, "--output");
        string? tessdata = Option(args, "--tessdata");
        string language = Option(args, "--language") ?? "eng";
        string manifestFull = Path.GetFullPath(manifestPath);
        string root = Path.GetDirectoryName(manifestFull)
            ?? throw new InvalidDataException("Manifest path has no parent directory.");
        string content = File.ReadAllText(manifestFull);
        var dataset = JsonSerializer.Deserialize<RecognitionDataset>(content, Json)
            ?? throw new InvalidDataException("Recognition dataset manifest was null.");
        RecognitionEvaluator.Validate(dataset);
        bool needsOcr = dataset.Frames.Any(x => x.Kind == "ocr");
        if (needsOcr && string.IsNullOrWhiteSpace(tessdata))
            throw new InvalidDataException("OCR datasets require --tessdata with installed traineddata models.");

        using JlOcr? ocr = needsOcr ? new JlOcr(tessdata!, language) : null;
        var oneD = new JlBarCode();
        var twoD = new JlDataCode2D();
        var outcomes = new List<RecognitionObservation>();
        foreach (var frame in dataset.Frames)
        {
            // Input paths are limited to the manifest directory. No remote images or
            // escaped relative paths are allowed, even for explicitly negative cases.
            string imagePath = ResolveInside(root, frame.File);
            var watch = Stopwatch.StartNew();
            bool detected = false, accepted = false, misread = false;
            int count = 0;
            string? error = null;
            string imageSha = "";
            try
            {
                imageSha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(imagePath))).ToLowerInvariant();
                using var image = new JlImage(imagePath);
                if (frame.Kind is "barcode1d" or "datacode2d")
                {
                    IReadOnlyList<JlRecognitionHit> hits = frame.Kind == "barcode1d"
                        ? frame.Roi is { } r1 ? oneD.Find(image, r1) : oneD.Find(image)
                        : frame.Roi is { } r2 ? twoD.Find(image, r2) : twoD.Find(image);
                    count = hits.Count;
                    detected = count > 0;
                    // One independently annotated target is expected per search ROI.
                    // Extra symbols are not silently treated as successful inspection.
                    accepted = frame.ExpectedPresent is true && count == 1 &&
                               hits[0].Text == frame.ExpectedText && hits[0].Format == frame.ExpectedFormat;
                }
                else
                {
                    var answer = ocr!.Recognize(image, frame.Roi, JlOcrPageMode.SingleLine);
                    detected = answer.HasText;
                    count = detected ? 1 : 0;
                    accepted = frame.ExpectedPresent is true &&
                               string.Equals(answer.Text, frame.ExpectedText, StringComparison.Ordinal);
                }
                misread = frame.ExpectedPresent is true && detected && !accepted;
            }
            catch (Exception ex)
            {
                // Decoder/input failures are recorded, not silently discarded from the
                // denominator. The acceptance gate rejects any processing error.
                error = ex.GetType().Name;
            }
            watch.Stop();
            outcomes.Add(new RecognitionObservation(frame.Id, frame.Kind, frame.Condition,
                frame.ExpectedPresent!.Value, detected, accepted, misread,
                watch.Elapsed.TotalMilliseconds, count, imageSha, error));
        }

        var report = RecognitionEvaluator.Evaluate(dataset, outcomes,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestFull))).ToLowerInvariant());
        string destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, JsonSerializer.Serialize(report, Json) + Environment.NewLine);
        Console.WriteLine($"Recognition dataset: {report.DatasetId}, {report.SourceType}; verdict: {report.Verdict}");
        Console.WriteLine($"TP={report.Overall.TruePositive} FN={report.Overall.FalseNegative} " +
            $"FP={report.Overall.FalsePositive} TN={report.Overall.TrueNegative} " +
            $"misreads={report.Overall.Misread} P95={report.Overall.P95Ms:F2} ms");
        foreach (string issue in report.Reasons) Console.WriteLine(" - " + issue);
        Console.WriteLine("Result written to " + destination);
        return report.Reasons.Count == 0 ? 0 : 2;
    }

    private static string ResolveInside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
            relative.Contains(':') || relative.Contains('\\'))
            throw new InvalidDataException("Image file must be a relative path under the manifest directory.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        string rel = Path.GetRelativePath(root, full);
        if (rel == ".." || rel.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(rel) || !File.Exists(full))
            throw new InvalidDataException("Image path escapes the dataset or is missing.");
        return full;
    }

    private static string RequiredOption(string[] args, string key) =>
        Option(args, key) ?? throw new ArgumentException("Required option missing: " + key);

    private static string? Option(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == key)
                return i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[i + 1] : throw new ArgumentException("No value after " + key);
        return null;
    }
}

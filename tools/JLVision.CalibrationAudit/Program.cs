using System;
using System.IO;
using System.Text.Json;
using JLVisionLib.Validation;

internal sealed class AuditManifest
{
    public PlanarAffineCalibration? Calibration { get; set; }
    public CalibrationEvidence? CalibrationEvidence { get; set; }
    public AcceptanceCriteria? Acceptance { get; set; }
}

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string manifest = RequiredOption(args, "--manifest");
            string output = OptionalOption(args, "--output")
                ?? "artifacts/calibration-audit.json";
            string manifestPath = Path.GetFullPath(manifest);
            var input = JsonSerializer.Deserialize<AuditManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Invalid calibration manifest.");

            if (input.Calibration is null || input.CalibrationEvidence is null ||
                input.Acceptance?.MaxCalibrationFitErrorMm is not double maxFit ||
                input.Acceptance.MaxCalibrationHoldoutErrorMm is not double maxHoldout ||
                input.Acceptance.MaxCalibrationHoldoutRmsMm is not double maxRms)
                throw new InvalidDataException(
                    "Calibration, calibrationEvidence and all three calibration residual limits are required.");

            input.Acceptance.Validate();
            CalibrationAuditResult result = CalibrationEvidenceAudit.Evaluate(
                input.Calibration, input.CalibrationEvidence, maxFit, maxHoldout, maxRms);

            string fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                inputManifest = Path.GetFileName(manifestPath),
                result,
                thresholds = new { maxFit, maxHoldout, maxHoldoutRmsMm = maxRms }
            }, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"Calibration ID: {result.CalibrationId}");
            Console.WriteLine($"Fit: {result.Fit.Count} points, max={result.Fit.MaxMm:F5} mm, RMS={result.Fit.RmsMm:F5} mm");
            Console.WriteLine($"Independent holdout: {result.Holdout.Count} points, max={result.Holdout.MaxMm:F5} mm, RMS={result.Holdout.RmsMm:F5} mm");
            Console.WriteLine($"Audit: {(result.Passed ? "PASS" : "FAIL")}");
            foreach (string reason in result.Reasons) Console.Error.WriteLine(reason);
            Console.WriteLine("JSON: " + fullOutput);
            return result.Passed ? 0 : 2;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static string RequiredOption(string[] args, string name)
        => OptionalOption(args, name) ??
           throw new ArgumentException($"Missing required argument {name}.");

    private static string? OptionalOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] != name) continue;
            if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
                throw new ArgumentException($"Missing argument value after {name}.");
            return args[i];
        }
        return null;
    }
}

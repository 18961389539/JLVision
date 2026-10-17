using System;
using System.IO;
using System.Text.Json;
using System.Globalization;
using JLVisionLib.Validation;

internal sealed class AuditManifest
{
    public PlanarAffineCalibration? Calibration { get; set; }
    public CalibrationEvidence? CalibrationEvidence { get; set; }
    public AcceptanceCriteria? Acceptance { get; set; }
    public DateTimeOffset? RecordedUtc { get; set; }
}

internal sealed class HistoryManifest
{
    public List<HistoryEntry> Snapshots { get; set; } = new();
}

internal sealed class HistoryEntry
{
    public string Manifest { get; set; } = string.Empty;
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
            AuditManifest input = ReadManifest(manifestPath);

            if (input.Calibration is null || input.CalibrationEvidence is null ||
                input.Acceptance?.MaxCalibrationFitErrorMm is not double maxFit ||
                input.Acceptance.MaxCalibrationHoldoutErrorMm is not double maxHoldout ||
                input.Acceptance.MaxCalibrationHoldoutRmsMm is not double maxRms)
                throw new InvalidDataException(
                    "Calibration, calibrationEvidence and all three calibration residual limits are required.");

            input.Acceptance.Validate();
            CalibrationAuditResult result = Audit(input, maxFit, maxHoldout, maxRms);
            string? baselinePath = OptionalOption(args, "--baseline");
            string? historyPath = OptionalOption(args, "--history");
            if (baselinePath is not null && historyPath is not null)
                throw new ArgumentException("--baseline and --history are mutually exclusive.");

            CalibrationVersionDelta? comparison = null;
            CalibrationHistoryResult? history = null;
            if (baselinePath is not null || historyPath is not null)
            {
                double maxPeakIncrease = RequiredNonnegativeDouble(args, "--max-peak-increase-mm");
                double maxRmsIncrease = RequiredNonnegativeDouble(args, "--max-rms-increase-mm");
                if (!input.RecordedUtc.HasValue)
                    throw new InvalidDataException("Comparison requires recordedUtc in the current manifest.");
                var now = new CalibrationSnapshot(input.RecordedUtc.Value, input.CalibrationEvidence, result);
                if (baselinePath is not null)
                {
                    string pastPath = Path.GetFullPath(baselinePath);
                    AuditManifest prior = ReadManifest(pastPath);
                    if (!prior.RecordedUtc.HasValue || prior.CalibrationEvidence is null)
                        throw new InvalidDataException("Baseline needs recordedUtc and independent evidence.");
                    comparison = CalibrationHistoryAnalysis.Compare(
                        new CalibrationSnapshot(prior.RecordedUtc.Value, prior.CalibrationEvidence,
                            Audit(prior, maxFit, maxHoldout, maxRms)),
                        now, maxPeakIncrease, maxRmsIncrease);
                }
                else
                {
                    string absoluteHistory = Path.GetFullPath(historyPath!);
                    HistoryManifest list = JsonSerializer.Deserialize<HistoryManifest>(
                        File.ReadAllText(absoluteHistory),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? throw new InvalidDataException("Invalid history index.");
                    if (list.Snapshots is null || list.Snapshots.Count < 1 || list.Snapshots.Count > 100)
                        throw new InvalidDataException("History index requires 1–100 earlier manifests.");
                    string root = Path.GetDirectoryName(absoluteHistory)!;
                    var snapshots = new List<CalibrationSnapshot>();
                    foreach (HistoryEntry entry in list.Snapshots)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Manifest))
                            throw new InvalidDataException("History entries must name a manifest.");
                        string priorPath = Path.GetFullPath(Path.IsPathRooted(entry.Manifest)
                            ? entry.Manifest : Path.Combine(root, entry.Manifest));
                        AuditManifest prior = ReadManifest(priorPath);
                        if (!prior.RecordedUtc.HasValue || prior.CalibrationEvidence is null)
                            throw new InvalidDataException("Historical snapshot needs recordedUtc and independent evidence.");
                        snapshots.Add(new CalibrationSnapshot(prior.RecordedUtc.Value,
                            prior.CalibrationEvidence, Audit(prior, maxFit, maxHoldout, maxRms)));
                    }
                    snapshots.Add(now);
                    history = CalibrationHistoryAnalysis.Analyze(
                        snapshots, maxPeakIncrease, maxRmsIncrease);
                }
            }

            string fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            string? svgOption = OptionalOption(args, "--heatmap");
            string? heatmapPath = null;
            if (svgOption is not null)
            {
                if (result.Spatial is null)
                    throw new InvalidDataException("Heatmap requires calibrationEvidence.imageRegion with explicit row/column bounds.");
                heatmapPath = Path.GetFullPath(svgOption);
                Directory.CreateDirectory(Path.GetDirectoryName(heatmapPath)!);
                File.WriteAllText(heatmapPath,
                    CalibrationHeatmapSvg.Render(result, maxHoldout));
            }

            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedUtc = DateTimeOffset.UtcNow,
                inputManifest = Path.GetFileName(manifestPath),
                heatmapFilename = heatmapPath is null ? null : Path.GetFileName(heatmapPath),
                comparison,
                history,
                result,
                thresholds = new { maxFit, maxHoldout, maxHoldoutRmsMm = maxRms }
            }, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"Calibration ID: {result.CalibrationId}");
            Console.WriteLine($"Fit: {result.Fit.Count} points, max={result.Fit.MaxMm:F5} mm, RMS={result.Fit.RmsMm:F5} mm");
            Console.WriteLine($"Independent holdout: {result.Holdout.Count} points, max={result.Holdout.MaxMm:F5} mm, RMS={result.Holdout.RmsMm:F5} mm");
            Console.WriteLine($"Audit: {(result.Passed ? "PASS" : "FAIL")}");
            if (result.Spatial is not null)
            {
                Console.WriteLine($"Spatial cells: fit {result.Spatial.FitOccupiedCells}/9, independent holdout {result.Spatial.HoldoutOccupiedCells}/9; outliers={result.Spatial.Outliers.Length}");
                Console.WriteLine($"Suggested additional holdout locations: {result.SuggestedValidationPoints.Length}");
            }
            if (comparison is not null)
                Console.WriteLine($"Version comparison: peak change {comparison.HoldoutMaxChangeMm:+0.00000;-0.00000;0} mm; RMS change {comparison.HoldoutRmsChangeMm:+0.00000;-0.00000;0} mm; regression={comparison.RegressionDetected}");
            if (history is not null)
                Console.WriteLine($"Calibration history: {history.Versions} versions; total peak change {history.NetHoldoutMaxChangeMm:+0.00000;-0.00000;0} mm; regression={history.RegressionDetected}");
            foreach (string reason in result.Reasons) Console.Error.WriteLine(reason);
            Console.WriteLine("JSON: " + fullOutput);
            if (heatmapPath is not null) Console.WriteLine("SVG: " + heatmapPath);
            return result.Passed && comparison?.RegressionDetected != true &&
                history?.RegressionDetected != true ? 0 : 2;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static AuditManifest ReadManifest(string path) =>
        JsonSerializer.Deserialize<AuditManifest>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Invalid calibration manifest: " + Path.GetFileName(path));

    private static CalibrationAuditResult Audit(AuditManifest input,
        double maxFit, double maxHoldout, double maxRms)
    {
        if (input.Calibration is null || input.CalibrationEvidence is null)
            throw new InvalidDataException("Manifest requires calibration and independent evidence.");
        input.Acceptance?.Validate();
        return CalibrationEvidenceAudit.Evaluate(
            input.Calibration, input.CalibrationEvidence, maxFit, maxHoldout, maxRms,
            input.Acceptance?.MinFitCalibrationGridCells,
            input.Acceptance?.MinHoldoutCalibrationGridCells);
    }

    private static double RequiredNonnegativeDouble(string[] args, string name)
    {
        string text = RequiredOption(args, name);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
            out double value) || !double.IsFinite(value) || value < 0)
            throw new ArgumentException($"{name} must be a finite nonnegative millimeter threshold.");
        return value;
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

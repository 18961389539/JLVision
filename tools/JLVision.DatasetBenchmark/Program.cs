using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JLVisionLib;
using JLVisionLib.Tests;
using JLVisionLib.Validation;

internal sealed class DatasetSpec
{
    public string Template { get; set; } = string.Empty;
    public double MinScore { get; set; } = 0.35;
    public PlanarAffineCalibration? Calibration { get; set; }
    public CalibrationEvidence? CalibrationEvidence { get; set; }
    public AcceptanceCriteria? Acceptance { get; set; }
    public List<DatasetFrame> Frames { get; set; } = new();
}

internal sealed class DatasetFrame
{
    public string File { get; set; } = string.Empty;
    public bool? ExpectedPresent { get; set; }
    public double? ExpectedRow { get; set; }
    public double? ExpectedColumn { get; set; }
    public double TolerancePixels { get; set; } = 5.0;
    public double? ExpectedAngleRad { get; set; }
    public double ToleranceAngleRad { get; set; } = 0.10;
    public double? ExpectedXmm { get; set; }
    public double? ExpectedYmm { get; set; }
    public string? RepeatGroup { get; set; }
}

internal static class Program
{
    private sealed record FrameResult(
        string Algorithm, string Filename, bool ExpectedPresent, bool Detected, bool Accepted,
        int MatchCount, double MatchMs, double? Row, double? Column, double? Angle,
        double? Score, double? PixelError, double? AngleErrorRad,
        double? Xmm, double? Ymm, double? PositionErrorMm, double? AngleErrorDeg,
        string? RepeatGroup);

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Native model datasets require Windows x64.");

        string? manifestPath = FindOption(args, "--manifest");
        string algorithm = (FindOption(args, "--algorithm") ?? "both").ToLowerInvariant();
        string output = FindOption(args, "--output") ?? "artifacts/model-dataset.json";
        if (algorithm != "ncc" && algorithm != "shape" && algorithm != "both")
            throw new ArgumentException("--algorithm must be ncc, shape or both.");

        using SyntheticVisionFixture? synthetic = manifestPath is null ? new SyntheticVisionFixture() : null;
        DatasetSpec dataset;
        string baseDir;
        if (manifestPath is null)
        {
            dataset = new DatasetSpec
            {
                Template = synthetic!.TemplatePath,
                Acceptance = new AcceptanceCriteria { MaxMissRate = 0, MaxFalsePositiveRate = 0 },
                Frames = new List<DatasetFrame>
                {
                    new() { File = synthetic.ScenePath, ExpectedPresent = true },
                    new() { File = synthetic.NegativeScenePath, ExpectedPresent = false }
                }
            };
            baseDir = Directory.GetCurrentDirectory();
        }
        else
        {
            string fullManifest = Path.GetFullPath(manifestPath);
            baseDir = Path.GetDirectoryName(fullManifest)!;
            dataset = JsonSerializer.Deserialize<DatasetSpec>(File.ReadAllText(fullManifest),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Dataset manifest was null.");
        }
        if (dataset.Frames is null || dataset.Frames.Count == 0 ||
            string.IsNullOrWhiteSpace(dataset.Template))
            throw new InvalidDataException("Manifest requires template and at least one frame.");
        if (dataset.MinScore <= 0 || dataset.MinScore > 1 || !double.IsFinite(dataset.MinScore))
            throw new InvalidDataException("MinScore must be finite and in (0, 1].");
        dataset.Calibration?.Validate();
        dataset.Acceptance?.Validate();
        if (dataset.Acceptance?.MaxPositionErrorMm is not null && dataset.Calibration is null)
            throw new InvalidDataException("Millimeter accuracy gate requires independently measured planar affine calibration.");
        if (dataset.Acceptance?.MaxRepeatabilityStdDevMm is not null && dataset.Calibration is null)
            throw new InvalidDataException("Millimeter repeatability gate requires planar affine calibration.");

        // Any mm/repeatability quality gate requires independent held-out evidence.
        // Never treat supplied coefficients alone as proof of metrology accuracy.
        bool mmGate = dataset.Acceptance?.MaxPositionErrorMm is not null ||
            dataset.Acceptance?.MaxRepeatabilityStdDevMm is not null;
        bool anyAuditLimit = dataset.Acceptance?.MaxCalibrationFitErrorMm is not null ||
            dataset.Acceptance?.MaxCalibrationHoldoutErrorMm is not null ||
            dataset.Acceptance?.MaxCalibrationHoldoutRmsMm is not null;
        if ((mmGate || anyAuditLimit || dataset.CalibrationEvidence is not null) && (dataset.Calibration is null ||
            dataset.CalibrationEvidence is null ||
            dataset.Acceptance?.MaxCalibrationFitErrorMm is null ||
            dataset.Acceptance.MaxCalibrationHoldoutErrorMm is null ||
            dataset.Acceptance.MaxCalibrationHoldoutRmsMm is null))
            throw new InvalidDataException("Calibrated mm acceptance requires calibration, independent fit/holdout evidence and explicit fit/holdout residual limits.");

        CalibrationAuditResult? calibrationAudit = dataset.Calibration is not null &&
            dataset.CalibrationEvidence is not null && dataset.Acceptance?.MaxCalibrationFitErrorMm is double fitLimit &&
            dataset.Acceptance.MaxCalibrationHoldoutErrorMm is double holdoutLimit &&
            dataset.Acceptance.MaxCalibrationHoldoutRmsMm is double holdoutRmsLimit
                ? CalibrationEvidenceAudit.Evaluate(dataset.Calibration, dataset.CalibrationEvidence,
                    fitLimit, holdoutLimit, holdoutRmsLimit) : null;

        string Resolve(string file) => Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(baseDir, file));
        JLVisionRuntime.Initialize();
        using var template = new JlImage(Resolve(dataset.Template));

        string[] algorithms = algorithm == "both" ? new[] { "ncc", "shape" } : new[] { algorithm };
        var results = new List<FrameResult>();
        var modelTrainingMs = new Dictionary<string, double>();

        foreach (string kind in algorithms)
        {
            long trainStart = Stopwatch.GetTimestamp();
            using JlNCCModel? ncc = kind == "ncc"
                ? new JlNCCModel(template, 3, -0.10, 0.20, 0.03, "use_polarity") : null;
            using JlShapeModel? shape = kind == "shape"
                ? new JlShapeModel(template, 3, -0.10, 0.20, 0.03,
                    "auto", "use_polarity", 20, 10) : null;
            modelTrainingMs[kind] = (Stopwatch.GetTimestamp() - trainStart) * 1000.0 / Stopwatch.Frequency;

            foreach (DatasetFrame frame in dataset.Frames)
            {
                if (string.IsNullOrWhiteSpace(frame.File) || frame.TolerancePixels < 0 ||
                    frame.ExpectedRow.HasValue != frame.ExpectedColumn.HasValue ||
                    !frame.ExpectedPresent.HasValue || frame.ToleranceAngleRad < 0 ||
                    !double.IsFinite(frame.TolerancePixels) || !double.IsFinite(frame.ToleranceAngleRad) ||
                    frame.ExpectedXmm.HasValue != frame.ExpectedYmm.HasValue ||
                    (frame.ExpectedAngleRad.HasValue && !frame.ExpectedPresent.Value) ||
                    ((frame.ExpectedRow.HasValue || frame.ExpectedXmm.HasValue) && !frame.ExpectedPresent.Value) ||
                    new double?[] { frame.ExpectedRow, frame.ExpectedColumn, frame.ExpectedAngleRad,
                        frame.ExpectedXmm, frame.ExpectedYmm }.Any(v => v.HasValue && !double.IsFinite(v.Value)))
                    throw new InvalidDataException("Each frame requires a file, explicit expectedPresent, nonnegative tolerances and complete coordinate pairs.");

                using var image = new JlImage(Resolve(frame.File));
                long matchStart = Stopwatch.GetTimestamp();

                JlTuple rows, cols, angles, scores;
                if (ncc is not null)
                {
                    ncc.FindNccModel(image, -0.10, 0.20, dataset.MinScore, 1, 0.5, "true", 0,
                        out rows, out cols, out angles, out scores);
                }
                else
                {
                    shape!.FindShapeModel(image, -0.10, 0.20, dataset.MinScore, 1, 0.5,
                        "least_squares", 0, 0.8,
                        out rows, out cols, out angles, out scores);
                }
                double matchMs = (Stopwatch.GetTimestamp() - matchStart) * 1000.0 / Stopwatch.Frequency;
                using (rows)
                using (cols)
                using (angles)
                using (scores)
                {
                    if (rows.Length != cols.Length || rows.Length != angles.Length ||
                        rows.Length != scores.Length)
                        throw new InvalidOperationException("Inconsistent native result vectors.");

                    bool found = rows.Length > 0;
                    double? row = found ? rows[0].D : null;
                    double? col = found ? cols[0].D : null;
                    double? angle = found ? angles[0].D : null;
                    double? score = found ? scores[0].D : null;
                    if (found && (!double.IsFinite(row!.Value) || !double.IsFinite(col!.Value) ||
                                  !double.IsFinite(angle!.Value) || !double.IsFinite(score!.Value) ||
                                  score.Value < dataset.MinScore || score.Value > 1.0))
                        throw new InvalidDataException("Native matcher returned nonfinite coordinates or an invalid score.");
                    double? error = null;
                    if (row.HasValue && col.HasValue && frame.ExpectedRow.HasValue)
                    {
                        double dr = row.Value - frame.ExpectedRow.Value;
                        double dc = col.Value - frame.ExpectedColumn!.Value;
                        error = Math.Sqrt(dr * dr + dc * dc);
                    }
                    double? angleError = angle.HasValue && frame.ExpectedAngleRad.HasValue
                        ? Math.Abs(Math.Atan2(Math.Sin(angle.Value - frame.ExpectedAngleRad.Value),
                            Math.Cos(angle.Value - frame.ExpectedAngleRad.Value))) : null;
                    (double Xmm, double Ymm)? measuredMm = row.HasValue && col.HasValue &&
                        dataset.Calibration is not null
                            ? dataset.Calibration.Transform(row.Value, col.Value) : null;
                    double? mmError = measuredMm.HasValue && frame.ExpectedXmm.HasValue
                        ? Math.Sqrt(Math.Pow(measuredMm.Value.Xmm - frame.ExpectedXmm.Value, 2) +
                                    Math.Pow(measuredMm.Value.Ymm - frame.ExpectedYmm!.Value, 2))
                        : null;
                    double? angleErrorDegrees = angleError.HasValue ? angleError.Value * 180.0 / Math.PI : null;
                    bool expected = frame.ExpectedPresent.Value;
                    bool accepted = expected == found &&
                        (!expected || !error.HasValue || error.Value <= frame.TolerancePixels) &&
                        (!expected || !angleError.HasValue || angleError.Value <= frame.ToleranceAngleRad);
                    results.Add(new FrameResult(kind, Path.GetFileName(frame.File),
                        expected, found, accepted, rows.Length,
                        matchMs, row, col, angle, score, error, angleError,
                        measuredMm?.Xmm, measuredMm?.Ymm, mmError, angleErrorDegrees,
                        frame.RepeatGroup));
                    Console.WriteLine($"{kind} {Path.GetFileName(frame.File)}: found={found} score={score:F3}, time={matchMs:F2} ms, accepted={accepted}");
                }
            }
        }

        var summary = algorithms.Select(name =>
        {
            FrameResult[] set = results.Where(r => r.Algorithm == name).ToArray();
            double[] timings = set.Select(r => r.MatchMs).OrderBy(v => v).ToArray();
            AlgorithmAcceptance analysis = VisionAcceptance.Analyze(set.Select(x =>
                new FrameMeasurement(x.ExpectedPresent, x.Detected, x.MatchMs,
                    x.PositionErrorMm, x.AngleErrorDeg, x.RepeatGroup, x.Xmm, x.Ymm))
                .ToArray(), dataset.Acceptance);
            return new
            {
                algorithm = name,
                acceptancePassed = analysis.Passed && set.All(x => x.Accepted) &&
                    (calibrationAudit is null || calibrationAudit.Passed),
                acceptance = analysis,
                frames = set.Length,
                correct = set.Count(r => r.Accepted),
                annotatedWorldMatches = set.Count(r => r.ExpectedPresent && r.PositionErrorMm.HasValue),
                unverifiedWorldMatches = set.Count(r => r.ExpectedPresent && r.Detected && !r.PositionErrorMm.HasValue),
                annotatedAngleMatches = set.Count(r => r.ExpectedPresent && r.AngleErrorDeg.HasValue),
                truePositive = set.Count(r => r.ExpectedPresent && r.Detected),
                trueNegative = set.Count(r => !r.ExpectedPresent && !r.Detected),
                falsePositive = set.Count(r => !r.ExpectedPresent && r.Detected),
                falseNegative = set.Count(r => r.ExpectedPresent && !r.Detected),
                localizationFailures = set.Count(r => r.ExpectedPresent && r.Detected && !r.Accepted),
                trainMs = modelTrainingMs[name],
                matchP50Ms = timings.Length == 0 ? 0 : timings[(timings.Length - 1) / 2],
                matchP95Ms = timings.Length == 0 ? 0 : timings[(int)Math.Ceiling((timings.Length - 1) * 0.95)]
            };
        }).ToArray();

        string fullOutput = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
        File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            syntheticFixture = synthetic is not null,
            datasetFrameCount = dataset.Frames.Count,
            minScore = dataset.MinScore,
            calibration = dataset.Calibration,
            calibrationAudit,
            acceptance = dataset.Acceptance,
            allAlgorithmsPassed = summary.All(x => x.acceptancePassed),
            summary,
            frames = results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Dataset report: {fullOutput}");

        return results.All(r => r.Accepted) &&
            summary.All(r => r.acceptancePassed) &&
            (calibrationAudit is null || calibrationAudit.Passed) ? 0 : 2;
    }

    private static string? FindOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name)
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing argument after {name}.");
                return args[i + 1];
            }
        return null;
    }
}

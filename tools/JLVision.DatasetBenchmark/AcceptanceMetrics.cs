using System;
using System.Collections.Generic;
using System.Linq;

namespace JLVisionLib.Validation;

/// <summary>
/// Fixed-camera planar pixel-to-world affine transform.
/// Column is image X and row is image Y; units of the output are millimeters.
/// This must be measured independently from real calibration correspondences.
/// </summary>
internal sealed class PlanarAffineCalibration
{
    public double XFromColumn { get; set; }
    public double XFromRow { get; set; }
    public double XOffset { get; set; }
    public double YFromColumn { get; set; }
    public double YFromRow { get; set; }
    public double YOffset { get; set; }

    public (double Xmm, double Ymm) Transform(double row, double column) =>
        (XFromColumn * column + XFromRow * row + XOffset,
         YFromColumn * column + YFromRow * row + YOffset);

    public void Validate()
    {
        double[] entries = { XFromColumn, XFromRow, XOffset, YFromColumn, YFromRow, YOffset };
        if (entries.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
            throw new ArgumentException("Calibration coefficients must be finite.");

        double determinant = XFromColumn * YFromRow - XFromRow * YFromColumn;
        if (Math.Abs(determinant) < 1e-12)
            throw new ArgumentException("Pixel-to-world affine calibration is singular.");
    }
}

internal sealed class AcceptanceCriteria
{
    public double? MaxPositionErrorMm { get; set; }
    public double? MaxAngleErrorDeg { get; set; }
    public double? MaxP95MatchMs { get; set; }
    public double? MaxMissRate { get; set; }
    public double? MaxFalsePositiveRate { get; set; }
    public double? MaxRepeatabilityStdDevMm { get; set; }
    public double? MaxCalibrationFitErrorMm { get; set; }
    public double? MaxCalibrationHoldoutErrorMm { get; set; }
    public double? MaxCalibrationHoldoutRmsMm { get; set; }
    public int? MinPositiveFrames { get; set; }
    public int? MinNegativeFrames { get; set; }

    public void Validate()
    {
        if ((MinPositiveFrames.HasValue && MinPositiveFrames.Value < 1) ||
            (MinNegativeFrames.HasValue && MinNegativeFrames.Value < 1))
            throw new ArgumentException("Minimum dataset population must be at least one frame.");

        foreach (var (name, value) in new[]
        {
            (nameof(MaxPositionErrorMm), MaxPositionErrorMm),
            (nameof(MaxAngleErrorDeg), MaxAngleErrorDeg),
            (nameof(MaxP95MatchMs), MaxP95MatchMs),
            (nameof(MaxRepeatabilityStdDevMm), MaxRepeatabilityStdDevMm),
            (nameof(MaxCalibrationFitErrorMm), MaxCalibrationFitErrorMm),
            (nameof(MaxCalibrationHoldoutErrorMm), MaxCalibrationHoldoutErrorMm),
            (nameof(MaxCalibrationHoldoutRmsMm), MaxCalibrationHoldoutRmsMm)
        })
        {
            if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0))
                throw new ArgumentException(name + " must be finite and nonnegative.");
        }
        foreach (var (name, value) in new[]
        {
            (nameof(MaxMissRate), MaxMissRate),
            (nameof(MaxFalsePositiveRate), MaxFalsePositiveRate)
        })
        {
            if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0 || value.Value > 1))
                throw new ArgumentException(name + " must lie between 0 and 1.");
        }
    }
}

internal sealed record FrameMeasurement(
    bool ExpectedPresent,
    bool Detected,
    double MatchMs,
    double? PositionErrorMm,
    double? AngleErrorDeg,
    string? RepeatGroup,
    double? Xmm,
    double? Ymm);

internal sealed record RepeatabilityStat(string Group, int Frames, double StdDevMm);

internal sealed record AcceptanceCheck(string Criterion, bool Passed, double? Observed, double Maximum, string Unit);

internal sealed class AlgorithmAcceptance
{
    public int TotalFrames { get; init; }
    public int TruePositive { get; init; }
    public int TrueNegative { get; init; }
    public int FalsePositive { get; init; }
    public int FalseNegative { get; init; }
    public double? Precision { get; init; }
    public double? Recall { get; init; }
    public double? MissRate { get; init; }
    public double? FalsePositiveRate { get; init; }
    public double P95MatchMs { get; init; }
    public double? MaxPositionErrorMmObserved { get; init; }
    public double? MaxAngleErrorDegObserved { get; init; }
    public RepeatabilityStat[] RepeatabilityGroups { get; init; } = Array.Empty<RepeatabilityStat>();
    public AcceptanceCheck[] Checks { get; init; } = Array.Empty<AcceptanceCheck>();
    public bool Passed => Checks.All(c => c.Passed);
}

internal static class VisionAcceptance
{
    public static double Percentile(IEnumerable<double> values, double quantile)
    {
        if (quantile < 0 || quantile > 1)
            throw new ArgumentOutOfRangeException(nameof(quantile));
        double[] sorted = values.OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
            throw new ArgumentException("Percentile requires at least one measurement.");
        return sorted[(int)Math.Ceiling((sorted.Length - 1) * quantile)];
    }

    public static double AngleErrorDegrees(double observedRad, double expectedRad) =>
        Math.Abs(Math.Atan2(Math.Sin(observedRad - expectedRad),
            Math.Cos(observedRad - expectedRad))) * 180.0 / Math.PI;

    public static RepeatabilityStat? Repeatability(string group, IReadOnlyList<FrameMeasurement> samples)
    {
        FrameMeasurement[] detections = samples.Where(s => s.Detected &&
            s.Xmm.HasValue && s.Ymm.HasValue).ToArray();
        if (detections.Length < 2)
            return null;
        double avgX = detections.Average(s => s.Xmm!.Value);
        double avgY = detections.Average(s => s.Ymm!.Value);
        double radialVariance = detections.Sum(s =>
        {
            double dx = s.Xmm!.Value - avgX;
            double dy = s.Ymm!.Value - avgY;
            return dx * dx + dy * dy;
        }) / (detections.Length - 1);
        return new RepeatabilityStat(group, detections.Length, Math.Sqrt(radialVariance));
    }

    public static AlgorithmAcceptance Analyze(IReadOnlyList<FrameMeasurement> data,
        AcceptanceCriteria? criteria)
    {
        if (data.Count == 0)
            throw new ArgumentException("Cannot analyze an empty dataset.");
        criteria?.Validate();
        int tp = data.Count(s => s.ExpectedPresent && s.Detected);
        int tn = data.Count(s => !s.ExpectedPresent && !s.Detected);
        int fp = data.Count(s => !s.ExpectedPresent && s.Detected);
        int fn = data.Count(s => s.ExpectedPresent && !s.Detected);
        int positives = tp + fn, negatives = tn + fp;
        double? missRate = positives > 0 ? (double)fn / positives : null;
        double? falsePositiveRate = negatives > 0 ? (double)fp / negatives : null;
        double? precision = tp + fp > 0 ? (double)tp / (tp + fp) : null;
        double? recall = positives > 0 ? (double)tp / positives : null;
        double? maximumMm = data.Where(s => s.ExpectedPresent && s.Detected &&
            s.PositionErrorMm.HasValue).Select(s => s.PositionErrorMm!.Value)
            .DefaultIfEmpty().Max();
        double? maximumAngle = data.Where(s => s.ExpectedPresent && s.Detected &&
            s.AngleErrorDeg.HasValue).Select(s => s.AngleErrorDeg!.Value)
            .DefaultIfEmpty().Max();

        var grouped = data.Where(s => !string.IsNullOrWhiteSpace(s.RepeatGroup))
            .GroupBy(s => s.RepeatGroup!, StringComparer.Ordinal).ToArray();
        var repeatStats = grouped
            .Select(g => Repeatability(g.Key, g.ToArray()))
            .Where(s => s is not null).Cast<RepeatabilityStat>().ToArray();

        var checks = new List<AcceptanceCheck>();
        if (criteria is not null)
        {
            void Add(string name, double? observed, double max, string unit) =>
                checks.Add(new AcceptanceCheck(name,
                    observed.HasValue && double.IsFinite(observed.Value) && observed.Value <= max,
                    observed, max, unit));

            if (criteria.MinPositiveFrames is int minPos)
                checks.Add(new AcceptanceCheck("minPositiveFrames", positives >= minPos,
                    positives, minPos, "frames"));
            if (criteria.MinNegativeFrames is int minNeg)
                checks.Add(new AcceptanceCheck("minNegativeFrames", negatives >= minNeg,
                    negatives, minNeg, "frames"));
            if (criteria.MaxMissRate is double maxMiss) Add("missRate", missRate, maxMiss, "fraction");
            if (criteria.MaxFalsePositiveRate is double maxFalse) Add("falsePositiveRate", falsePositiveRate, maxFalse, "fraction");
            if (criteria.MaxP95MatchMs is double maxP95) Add("p95MatchMs",
                Percentile(data.Select(s => s.MatchMs), .95), maxP95, "ms");
            if (criteria.MaxPositionErrorMm is double maxMm)
            {
                // Require ground truth for ALL positive frames (including missed targets).
                bool annotated = positives > 0 && data.Where(s => s.ExpectedPresent)
                    .All(s => s.Detected && s.PositionErrorMm.HasValue);
                Add("positionErrorMm", annotated && tp > 0 ? maximumMm : null, maxMm, "mm");
            }
            if (criteria.MaxAngleErrorDeg is double maxAngle)
            {
                bool annotated = positives > 0 && data.Where(s => s.ExpectedPresent)
                    .All(s => s.Detected && s.AngleErrorDeg.HasValue);
                Add("angleErrorDeg", annotated && tp > 0 ? maximumAngle : null, maxAngle, "deg");
            }
            if (criteria.MaxRepeatabilityStdDevMm is double maxRepeat)
            {
                // Every declared repeat group needs at least two valid detections.
                double? observed = grouped.Length > 0 &&
                    repeatStats.Length == grouped.Length &&
                    grouped.All(g => g.Count() >= 2 && g.All(s => s.ExpectedPresent &&
                        s.Detected && s.Xmm.HasValue && s.Ymm.HasValue))
                    ? repeatStats.Max(s => s.StdDevMm) : null;
                Add("repeatabilityStdDevMm", observed, maxRepeat, "mm");
            }
        }
        return new AlgorithmAcceptance
        {
            TotalFrames = data.Count,
            TruePositive = tp,
            TrueNegative = tn,
            FalsePositive = fp,
            FalseNegative = fn,
            Precision = precision,
            Recall = recall,
            MissRate = missRate,
            FalsePositiveRate = falsePositiveRate,
            P95MatchMs = Percentile(data.Select(s => s.MatchMs), .95),
            MaxPositionErrorMmObserved = data.Any(s => s.ExpectedPresent && s.Detected &&
                s.PositionErrorMm.HasValue) ? maximumMm : null,
            MaxAngleErrorDegObserved = data.Any(s => s.ExpectedPresent && s.Detected &&
                s.AngleErrorDeg.HasValue) ? maximumAngle : null,
            RepeatabilityGroups = repeatStats,
            Checks = checks.ToArray()
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace JLVisionLib.Validation;

/// <summary>
/// A point-in-time result with the original measurement references retained.
/// Comparisons never infer trends from different fields of view or from different
/// reference targets with incomparable locations.
/// </summary>
internal sealed record CalibrationSnapshot(DateTimeOffset RecordedUtc,
    CalibrationEvidence Evidence, CalibrationAuditResult Audit);

internal sealed record CalibrationVersionDelta(
    string BaselineId, string CurrentId,
    double HoldoutMaxChangeMm, double HoldoutRmsChangeMm,
    double? FitMaxChangeMm, int HoldoutGridCellChange,
    bool RegressionDetected, string[] Reasons);

internal sealed record CalibrationHistoryResult(
    int Versions, string FirstId, string LastId, double NetHoldoutMaxChangeMm,
    double NetHoldoutRmsChangeMm, double WorstStepHoldoutMaxIncreaseMm,
    double WorstStepHoldoutRmsIncreaseMm, int DegradingSteps,
    bool RegressionDetected, CalibrationVersionDelta[] Steps);

internal static class CalibrationHistoryAnalysis
{
    public static CalibrationVersionDelta Compare(CalibrationSnapshot baseline,
        CalibrationSnapshot current, double maxPeakIncreaseMm, double maxRmsIncreaseMm)
    {
        ValidateThreshold(maxPeakIncreaseMm);
        ValidateThreshold(maxRmsIncreaseMm);
        VerifyComparable(baseline, current);
        double peakChange = current.Audit.Holdout.MaxMm - baseline.Audit.Holdout.MaxMm;
        double rmsChange = current.Audit.Holdout.RmsMm - baseline.Audit.Holdout.RmsMm;
        var reasons = new List<string>();
        if (peakChange > maxPeakIncreaseMm + 1e-12)
            reasons.Add("held-out maximum residual increased beyond limit");
        if (rmsChange > maxRmsIncreaseMm + 1e-12)
            reasons.Add("held-out RMS residual increased beyond limit");
        // Fit datasets can change between calibrations. Their errors cannot
        // be compared unless the physical fit correspondences are identical.
        double? fitDelta = SameCorrespondences(
            baseline.Evidence.FitPoints, current.Evidence.FitPoints)
            ? current.Audit.Fit.MaxMm - baseline.Audit.Fit.MaxMm : null;
        return new CalibrationVersionDelta(
            baseline.Audit.CalibrationId, current.Audit.CalibrationId,
            peakChange, rmsChange,
            fitDelta,
            current.Audit.Spatial!.HoldoutOccupiedCells -
                baseline.Audit.Spatial!.HoldoutOccupiedCells,
            reasons.Count > 0, reasons.ToArray());
    }

    public static CalibrationHistoryResult Analyze(
        IReadOnlyList<CalibrationSnapshot> snapshots,
        double maxPeakIncreaseMm, double maxRmsIncreaseMm)
    {
        if (snapshots is null || snapshots.Count < 2)
            throw new ArgumentException("History requires at least two independently recorded versions.");
        ValidateThreshold(maxPeakIncreaseMm);
        ValidateThreshold(maxRmsIncreaseMm);

        for (int i = 1; i < snapshots.Count; ++i)
        {
            if (snapshots[i].RecordedUtc <= snapshots[i - 1].RecordedUtc)
                throw new ArgumentException("History timestamps must be strictly increasing.");
            VerifyComparable(snapshots[0], snapshots[i]);
        }
        if (snapshots.Select(x => x.Audit.CalibrationId).Distinct(StringComparer.Ordinal).Count()
            != snapshots.Count)
            throw new ArgumentException("History must contain unique calibration version IDs.");

        var steps = new List<CalibrationVersionDelta>();
        for (int i = 1; i < snapshots.Count; ++i)
            steps.Add(Compare(snapshots[i - 1], snapshots[i],
                maxPeakIncreaseMm, maxRmsIncreaseMm));
        // An accumulation of small changes can still exceed the total budget,
        // even when no individual step crosses the per-step threshold.
        var total = Compare(snapshots[0], snapshots[^1],
            maxPeakIncreaseMm, maxRmsIncreaseMm);
        return new CalibrationHistoryResult(snapshots.Count,
            snapshots[0].Audit.CalibrationId, snapshots[^1].Audit.CalibrationId,
            total.HoldoutMaxChangeMm, total.HoldoutRmsChangeMm,
            steps.Max(x => x.HoldoutMaxChangeMm),
            steps.Max(x => x.HoldoutRmsChangeMm),
            steps.Count(x => x.HoldoutMaxChangeMm > 0 || x.HoldoutRmsChangeMm > 0),
            total.RegressionDetected || steps.Any(x => x.RegressionDetected),
            steps.ToArray());
    }

    private static void ValidateThreshold(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Regression tolerance must be finite and nonnegative (mm).");
    }

    private static void VerifyComparable(CalibrationSnapshot baseline,
        CalibrationSnapshot current)
    {
        if (baseline.Audit.Spatial?.ImageRegion is not CalibrationImageRegion a ||
            current.Audit.Spatial?.ImageRegion is not CalibrationImageRegion b)
            throw new ArgumentException("Version comparison needs the same explicit image region.");
        if (a.MinRow != b.MinRow || a.MaxRow != b.MaxRow ||
            a.MinColumn != b.MinColumn || a.MaxColumn != b.MaxColumn)
            throw new ArgumentException("Cannot compare calibration across different image regions.");
        if (!string.Equals(baseline.Audit.MeasurementMethod,
            current.Audit.MeasurementMethod, StringComparison.Ordinal))
            throw new ArgumentException("Cannot compare calibrations using different metrology methods.");
        if (baseline.RecordedUtc >= current.RecordedUtc)
            throw new ArgumentException("Baseline version must precede current version.");
        // The same independent physical reference measurements must be used
        // to assess calibration drift. Different targets would change apparent
        // residuals without proving a change in calibration quality.
        if (!SameCorrespondences(baseline.Evidence.ValidationPoints,
            current.Evidence.ValidationPoints))
            throw new ArgumentException(
                "Reference holdout correspondences differ; compare only identical independently measured points.");
    }
    private static bool SameCorrespondences(
        IReadOnlyList<CalibrationPoint> left, IReadOnlyList<CalibrationPoint> right)
    {
        if (left.Count != right.Count) return false;
        var a = left.OrderBy(p => p.Row).ThenBy(p => p.Column).ToArray();
        var b = right.OrderBy(p => p.Row).ThenBy(p => p.Column).ToArray();
        for (int i = 0; i < a.Length; ++i)
            if (a[i].Row != b[i].Row || a[i].Column != b[i].Column ||
                a[i].Xmm != b[i].Xmm || a[i].Ymm != b[i].Ymm)
                return false;
        return true;
    }

}

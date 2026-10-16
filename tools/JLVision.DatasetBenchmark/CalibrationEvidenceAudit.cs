using System;
using System.Collections.Generic;
using System.Linq;

namespace JLVisionLib.Validation;

/// <summary>
/// Evidence from an external pixel/world calibration procedure. The coefficients
/// are supplied separately; this audit verifies them against measured control
/// points and independent holdout points rather than fitting on test frames.
/// </summary>
internal sealed class CalibrationEvidence
{
    public string? CalibrationId { get; set; }
    public string? MeasurementMethod { get; set; }
    public List<CalibrationPoint> FitPoints { get; set; } = new();
    public List<CalibrationPoint> ValidationPoints { get; set; } = new();
}

internal sealed class CalibrationPoint
{
    public double Row { get; set; }
    public double Column { get; set; }
    public double Xmm { get; set; }
    public double Ymm { get; set; }
}

internal sealed record CalibrationResidual(double Row, double Column,
    double ErrorXmm, double ErrorYmm, double RadialErrorMm);

internal sealed record CalibrationResidualSummary(int Count, double RmsMm,
    double MaxMm, CalibrationResidual[] Residuals);

internal sealed record CalibrationAuditResult(string CalibrationId, string MeasurementMethod,
    CalibrationResidualSummary Fit, CalibrationResidualSummary Holdout,
    bool Passed, string[] Reasons);

internal static class CalibrationEvidenceAudit
{
    public static CalibrationAuditResult Evaluate(PlanarAffineCalibration calibration,
        CalibrationEvidence evidence, double maxFitErrorMm, double maxHoldoutErrorMm,
        double maxHoldoutRmsMm)
    {
        calibration.Validate();
        if (evidence is null)
            throw new ArgumentNullException(nameof(evidence));
        foreach (double limit in new[] { maxFitErrorMm, maxHoldoutErrorMm, maxHoldoutRmsMm })
            if (!double.IsFinite(limit) || limit < 0)
                throw new ArgumentException("All residual limits must be finite and nonnegative.");
        if (string.IsNullOrWhiteSpace(evidence.CalibrationId) ||
            string.IsNullOrWhiteSpace(evidence.MeasurementMethod))
            throw new ArgumentException("Calibration evidence requires a nonblank ID and measurement method.");
        if (evidence.FitPoints is null || evidence.FitPoints.Count < 4 ||
            evidence.ValidationPoints is null || evidence.ValidationPoints.Count < 3)
            throw new ArgumentException("Need at least four fit points and three independent holdout points.");

        CalibrationPoint[] all = evidence.FitPoints.Concat(evidence.ValidationPoints).ToArray();
        foreach (CalibrationPoint point in all)
        {
            if (point is null || !double.IsFinite(point.Row) || !double.IsFinite(point.Column) ||
                !double.IsFinite(point.Xmm) || !double.IsFinite(point.Ymm))
                throw new ArgumentException("Calibration evidence contains a null or nonfinite measured point.");
        }

        // A repeated pixel location in training or held-out validation is not
        // an independent correspondence and can artificially lower RMS error.
        var imagePoints = new HashSet<(double Row, double Column)>();
        foreach (CalibrationPoint point in all)
        {
            if (!imagePoints.Add((point.Row, point.Column)))
                throw new ArgumentException("Fit and holdout points must have distinct pixel coordinates.");
        }

        if (!HasNonCollinearTriangle(evidence.FitPoints))
            throw new ArgumentException("Fit points are collinear or nearly degenerate.");

        CalibrationResidualSummary fit = Summarize(calibration, evidence.FitPoints);
        CalibrationResidualSummary holdout = Summarize(calibration, evidence.ValidationPoints);
        string[] reasons = new[]
        {
            fit.MaxMm > maxFitErrorMm ? "fit residual exceeds limit" : null,
            holdout.MaxMm > maxHoldoutErrorMm ? "holdout maximum residual exceeds limit" : null,
            holdout.RmsMm > maxHoldoutRmsMm ? "holdout RMS residual exceeds limit" : null
        }.Where(x => x is not null).Select(x => x!).ToArray();

        return new CalibrationAuditResult(evidence.CalibrationId!, evidence.MeasurementMethod!,
            fit, holdout, reasons.Length == 0, reasons);
    }

    private static CalibrationResidualSummary Summarize(
        PlanarAffineCalibration calibration, IReadOnlyCollection<CalibrationPoint> points)
    {
        CalibrationResidual[] residuals = points.Select(point =>
        {
            var measured = calibration.Transform(point.Row, point.Column);
            double dx = measured.Xmm - point.Xmm;
            double dy = measured.Ymm - point.Ymm;
            double error = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(error))
                throw new ArgumentException("Calibration computation overflowed.");
            return new CalibrationResidual(point.Row, point.Column, dx, dy, error);
        }).ToArray();

        return new CalibrationResidualSummary(residuals.Length,
            Math.Sqrt(residuals.Average(p => p.RadialErrorMm * p.RadialErrorMm)),
            residuals.Max(p => p.RadialErrorMm), residuals);
    }

    private static bool HasNonCollinearTriangle(IReadOnlyList<CalibrationPoint> points)
    {
        double minCol = points.Min(p => p.Column), maxCol = points.Max(p => p.Column);
        double minRow = points.Min(p => p.Row), maxRow = points.Max(p => p.Row);
        double spanSq = Math.Pow(maxCol - minCol, 2) + Math.Pow(maxRow - minRow, 2);
        if (!double.IsFinite(spanSq) || spanSq < 1e-10)
            return false;
        for (int i = 0; i < points.Count; ++i)
        for (int j = i + 1; j < points.Count; ++j)
        for (int k = j + 1; k < points.Count; ++k)
        {
            double x1 = points[j].Column - points[i].Column;
            double y1 = points[j].Row - points[i].Row;
            double x2 = points[k].Column - points[i].Column;
            double y2 = points[k].Row - points[i].Row;
            double area2 = Math.Abs(x1 * y2 - y1 * x2);
            if (area2 / spanSq >= 1e-4)
                return true;
        }
        return false;
    }
}

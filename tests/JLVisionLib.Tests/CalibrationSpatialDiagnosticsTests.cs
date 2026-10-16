using System;
using System.Collections.Generic;
using System.Linq;
using JLVisionLib.Validation;
using Xunit;

namespace JLVisionLib.Tests;

public sealed class CalibrationSpatialDiagnosticsTests
{
    private static PlanarAffineCalibration Calibration() => new()
    {
        XFromColumn = 0.05,
        YFromRow = 0.05
    };

    private static CalibrationPoint P(double row, double column, double dx = 0, double dy = 0) =>
        new() { Row = row, Column = column, Xmm = column * 0.05 + dx, Ymm = row * 0.05 + dy };

    private static CalibrationEvidence Evidence() => new()
    {
        CalibrationId = "Synthetic and independently labeled fixture",
        MeasurementMethod = "unit-test coordinates, not physical measurements",
        ImageRegion = new CalibrationImageRegion
        {
            MinRow = 0, MaxRow = 120, MinColumn = 0, MaxColumn = 100
        },
        FitPoints = new List<CalibrationPoint>
        {
            P(0, 0), P(0, 100), P(100, 0), P(100, 100), P(50, 25)
        },
        ValidationPoints = new List<CalibrationPoint>
        {
            P(20, 40), P(75, 80), P(120, 20)
        }
    };

    [Fact]
    public void DistinctMeasuredCellsAreCountedAndEmptyCellsStayUnknown()
    {
        var report = CalibrationEvidenceAudit.Evaluate(Calibration(), Evidence(), .02, .05, .05,
            minFitGridCells: 5, minHoldoutGridCells: 3);
        Assert.True(report.Passed);
        Assert.NotNull(report.Spatial);
        Assert.Equal(5, report.Spatial!.FitOccupiedCells);
        Assert.Equal(3, report.Spatial.HoldoutOccupiedCells);
        Assert.Equal(9, report.Spatial.Cells.Length);
        Assert.Equal(6, report.Spatial.Cells.Count(cell => cell.HoldoutCount == 0));
        Assert.All(report.Spatial.Cells.Where(cell => cell.HoldoutCount == 0),
            cell => Assert.Null(cell.HoldoutMaxErrorMm));
        Assert.Empty(report.Spatial.Outliers);
    }

    [Fact]
    public void CoverageGateFailsEvenWhenEveryMeasuredResidualIsZero()
    {
        var result = CalibrationEvidenceAudit.Evaluate(Calibration(), Evidence(), .02, .05, .05,
            minFitGridCells: 5, minHoldoutGridCells: 4);
        Assert.False(result.Passed);
        Assert.Equal(0, result.Holdout.MaxMm);
        Assert.Contains(result.Reasons, reason => reason.Contains("holdout spatial coverage"));
    }

    [Fact]
    public void ThresholdViolationsIdentifyTheExactOutlierPositionAndPopulation()
    {
        var evidence = Evidence();
        evidence.ValidationPoints[1].Xmm += .08;
        var result = CalibrationEvidenceAudit.Evaluate(Calibration(), evidence, .02, .05, .05);
        Assert.False(result.Passed);
        var outlier = Assert.Single(result.Spatial!.Outliers);
        Assert.Equal("holdout", outlier.Population);
        Assert.Equal(1, outlier.PointIndex);
        Assert.Equal(75, outlier.Row);
        Assert.Equal(80, outlier.Column);
        Assert.InRange(outlier.RadialErrorMm, .0799, .0801);
        Assert.InRange(outlier.ErrorXmm, -.081, -.079);
    }

    [Fact]
    public void SpatialCoverageGateRequiresDeclaredImageRegion()
    {
        var evidence = Evidence();
        evidence.ImageRegion = null;
        Assert.Throws<ArgumentException>(() =>
            CalibrationEvidenceAudit.Evaluate(Calibration(), evidence, .02, .05, .05,
                minHoldoutGridCells: 1));
    }

    [Fact]
    public void OutOfFramePointIsRejectedRatherThanSilentlyClamped()
    {
        var evidence = Evidence();
        evidence.ValidationPoints[0].Row = 150;
        Assert.Throws<ArgumentException>(() =>
            CalibrationEvidenceAudit.Evaluate(Calibration(), evidence, .02, .05, .05));
    }

    [Fact]
    public void SvgRendersOnlyObservedCellsAndEscapesUntrustedCalibrationId()
    {
        var evidence = Evidence();
        evidence.CalibrationId = "Calibration <A & B>";
        var audit = CalibrationEvidenceAudit.Evaluate(Calibration(), evidence, .02, .05, .05);
        string svg = CalibrationHeatmapSvg.Render(audit, .05);
        Assert.Contains("<svg", svg);
        Assert.Contains("Calibration &lt;A &amp; B&gt;", svg);
        Assert.Contains("NO HOLDOUT", svg);
        Assert.Contains("not extrapolated accuracy", svg);
        Assert.Contains("Holdout cells: 3/9", svg);
        Assert.DoesNotContain("<script", svg);
    }

    [Fact]
    public void SvgRequiresMeasuredImageBoundsInsteadOfInventingCoverage()
    {
        var evidence = Evidence();
        evidence.ImageRegion = null;
        var audit = CalibrationEvidenceAudit.Evaluate(Calibration(), evidence, .02, .05, .05);
        Assert.Throws<ArgumentException>(() => CalibrationHeatmapSvg.Render(audit, .05));
    }

    [Fact]
    public void InvalidSpatialLimitsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalibrationEvidenceAudit.Evaluate(Calibration(), Evidence(), .02, .05, .05,
                minFitGridCells: 10));
        Assert.Throws<ArgumentException>(() => new AcceptanceCriteria
        {
            MinHoldoutCalibrationGridCells = 0
        }.Validate());
    }
}

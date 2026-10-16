using System;
using System.Collections.Generic;
using JLVisionLib.Validation;
using Xunit;

namespace JLVisionLib.Tests;

public sealed class CalibrationEvidenceAuditTests
{
    private static PlanarAffineCalibration KnownCalibration() => new()
    {
        XFromColumn = 0.05, XFromRow = 0.002, XOffset = 10,
        YFromColumn = -0.003, YFromRow = -0.04, YOffset = 100
    };

    private static CalibrationPoint Point(PlanarAffineCalibration calibration,
        double row, double column)
    {
        var (x, y) = calibration.Transform(row, column);
        return new CalibrationPoint { Row = row, Column = column, Xmm = x, Ymm = y };
    }

    private static CalibrationEvidence KnownEvidence(PlanarAffineCalibration calibration) => new()
    {
        CalibrationId = "fixture-2026-01",
        MeasurementMethod = "synthetic independent metrology fixture (unit test only)",
        FitPoints = new List<CalibrationPoint>
        {
            Point(calibration, 0, 0), Point(calibration, 0, 100),
            Point(calibration, 100, 0), Point(calibration, 100, 100),
            Point(calibration, 50, 25)
        },
        ValidationPoints = new List<CalibrationPoint>
        {
            Point(calibration, 20, 40), Point(calibration, 75, 80),
            Point(calibration, 120, 20)
        }
    };

    [Fact]
    public void IndependentHoldoutResidualsPassForKnownAffineGeometry()
    {
        var calibration = KnownCalibration();
        var evidence = KnownEvidence(calibration);
        var result = CalibrationEvidenceAudit.Evaluate(calibration, evidence, .01, .01, .01);
        Assert.True(result.Passed);
        Assert.Equal(5, result.Fit.Count);
        Assert.Equal(3, result.Holdout.Count);
        Assert.InRange(result.Holdout.MaxMm, 0, 1e-10);
        Assert.InRange(result.Holdout.RmsMm, 0, 1e-10);
    }

    [Fact]
    public void IndependentHoldoutDetectsAWellFitButUnreliableCalibration()
    {
        var calibration = KnownCalibration();
        var evidence = KnownEvidence(calibration);
        evidence.ValidationPoints[1].Xmm += 0.08;
        var result = CalibrationEvidenceAudit.Evaluate(calibration, evidence, .01, .05, .05);
        Assert.False(result.Passed);
        Assert.Contains(result.Reasons, r => r.Contains("holdout maximum"));
        Assert.InRange(result.Fit.MaxMm, 0, 1e-10);
        Assert.InRange(result.Holdout.MaxMm, .079, .081);
    }

    [Fact]
    public void HoldoutMustNotReuseTrainingCoordinates()
    {
        var calibration = KnownCalibration();
        var evidence = KnownEvidence(calibration);
        evidence.ValidationPoints[0] = Point(calibration, 0, 100);
        Assert.Throws<ArgumentException>(() =>
            CalibrationEvidenceAudit.Evaluate(calibration, evidence, .1, .1, .1));
    }

    [Fact]
    public void DegenerateCalibrationFitIsRejected()
    {
        var calibration = KnownCalibration();
        var evidence = KnownEvidence(calibration);
        evidence.FitPoints = new List<CalibrationPoint>
        {
            Point(calibration, 0, 0), Point(calibration, 10, 10),
            Point(calibration, 20, 20), Point(calibration, 30, 30)
        };
        Assert.Throws<ArgumentException>(() =>
            CalibrationEvidenceAudit.Evaluate(calibration, evidence, .1, .1, .1));
    }

    [Fact]
    public void ImplausibleCoefficientsFailIndependentValidation()
    {
        var truth = KnownCalibration();
        var evidence = KnownEvidence(truth);
        var wrong = KnownCalibration();
        wrong.XOffset += 0.2;
        var result = CalibrationEvidenceAudit.Evaluate(wrong, evidence, .1, .1, .1);
        Assert.False(result.Passed);
        Assert.True(result.Holdout.MaxMm > .19);
        Assert.NotEmpty(result.Reasons);
    }

    [Fact]
    public void EvidenceProvenanceAndMinimumPointCountsAreMandatory()
    {
        var calibration = KnownCalibration();
        var evidence = KnownEvidence(calibration);
        evidence.CalibrationId = null;
        Assert.Throws<ArgumentException>(() =>
            CalibrationEvidenceAudit.Evaluate(calibration, evidence, .1, .1, .1));
        evidence.CalibrationId = "test";
        evidence.ValidationPoints.RemoveAt(0);
        Assert.Throws<ArgumentException>(() =>
            CalibrationEvidenceAudit.Evaluate(calibration, evidence, .1, .1, .1));
    }
}

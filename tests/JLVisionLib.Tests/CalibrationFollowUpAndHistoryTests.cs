using System;
using System.Collections.Generic;
using System.Linq;
using JLVisionLib.Validation;
using Xunit;

namespace JLVisionLib.Tests;

public sealed class CalibrationFollowUpAndHistoryTests
{
    private static PlanarAffineCalibration Calibration(double xOffset = 0) => new()
    {
        XFromColumn = .05, YFromRow = .05, XOffset = xOffset
    };

    private static CalibrationPoint Point(double row, double column) =>
        new() { Row = row, Column = column, Xmm = column * .05, Ymm = row * .05 };

    private static CalibrationEvidence Evidence(string id) => new()
    {
        CalibrationId = id,
        MeasurementMethod = "fixed independent synthetic reference grid",
        ImageRegion = new CalibrationImageRegion
        {
            MinRow = 0, MaxRow = 120,
            MinColumn = 0, MaxColumn = 100
        },
        FitPoints = new List<CalibrationPoint>
        {
            Point(0, 0), Point(0, 100), Point(100, 0),
            Point(100, 100), Point(50, 25)
        },
        ValidationPoints = new List<CalibrationPoint>
        {
            Point(20, 40), Point(75, 80), Point(120, 20)
        }
    };

    private static CalibrationSnapshot Snapshot(string id, int day, double offset)
    {
        var evidence = Evidence(id);
        var audit = CalibrationEvidenceAudit.Evaluate(Calibration(offset), evidence,
            .2, .2, .2);
        return new CalibrationSnapshot(
            new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero), evidence, audit);
    }

    [Fact]
    public void RecommendationsPrioritizeUnmeasuredHoldoutCellsAndAvoidExistingPoints()
    {
        var evidence = Evidence("v1");
        var audit = CalibrationEvidenceAudit.Evaluate(Calibration(), evidence,
            .1, .1, .1);
        var suggestions = audit.SuggestedValidationPoints;
        Assert.Equal(9, suggestions.Length);
        Assert.All(suggestions.Take(6), x =>
        {
            Assert.Equal(0, x.ExistingHoldoutCount);
            Assert.Contains("unverified", x.Reason);
        });
        Assert.Equal(9, suggestions.Select(x => (x.GridRow, x.GridColumn)).Distinct().Count());
        foreach (var candidate in suggestions)
        {
            Assert.InRange(candidate.Row, 0, 120);
            Assert.InRange(candidate.Column, 0, 100);
            Assert.DoesNotContain(evidence.FitPoints.Concat(evidence.ValidationPoints),
                p => Math.Pow(p.Row - candidate.Row, 2) +
                     Math.Pow(p.Column - candidate.Column, 2) < 1);
        }
    }

    [Fact]
    public void NoImageRoiMeansNoPretendedRecommendations()
    {
        var evidence = Evidence("v1");
        evidence.ImageRegion = null;
        var audit = CalibrationEvidenceAudit.Evaluate(Calibration(), evidence,
            .1, .1, .1);
        Assert.Empty(audit.SuggestedValidationPoints);
    }

    [Fact]
    public void RecommendationCountCanBeLimited()
    {
        var evidence = Evidence("v1");
        var audit = CalibrationEvidenceAudit.Evaluate(Calibration(), evidence,
            .1, .1, .1);
        var result = CalibrationFollowUpPlanner.Suggest(audit.Spatial,
            evidence.FitPoints, evidence.ValidationPoints, 2);
        Assert.Equal(2, result.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalibrationFollowUpPlanner.Suggest(audit.Spatial,
                evidence.FitPoints, evidence.ValidationPoints, 10));
    }

    [Fact]
    public void ComparisonFindsWorseningHeldOutResiduals()
    {
        var delta = CalibrationHistoryAnalysis.Compare(
            Snapshot("baseline", 1, 0),
            Snapshot("current", 2, .06), .05, .05);
        Assert.True(delta.RegressionDetected);
        Assert.InRange(delta.HoldoutMaxChangeMm, .059, .061);
        Assert.InRange(delta.HoldoutRmsChangeMm, .059, .061);
        Assert.Contains(delta.Reasons, x => x.Contains("RMS"));
    }

    [Fact]
    public void ImprovingResidualsDoNotRaiseRegression()
    {
        var delta = CalibrationHistoryAnalysis.Compare(
            Snapshot("baseline", 1, .08),
            Snapshot("current", 2, .01), .01, .01);
        Assert.False(delta.RegressionDetected);
        Assert.True(delta.HoldoutRmsChangeMm < 0);
    }

    [Fact]
    public void HistoryDetectsAccumulatedSmallDrift()
    {
        var report = CalibrationHistoryAnalysis.Analyze(
            new[]
            {
                Snapshot("v1", 1, 0),
                Snapshot("v2", 2, .02),
                Snapshot("v3", 3, .04)
            }, .03, .03);
        Assert.True(report.RegressionDetected);
        Assert.Equal(3, report.Versions);
        Assert.Equal(2, report.Steps.Length);
        Assert.All(report.Steps, delta => Assert.False(delta.RegressionDetected));
        Assert.InRange(report.NetHoldoutMaxChangeMm, .039, .041);
        Assert.Equal(2, report.DegradingSteps);
    }

    [Fact]
    public void HistoryRejectsChangedHoldoutReferenceCoordinates()
    {
        var first = Snapshot("v1", 1, 0);
        var second = Snapshot("v2", 2, .02);
        second.Evidence.ValidationPoints[0].Xmm += .01;
        Assert.Throws<ArgumentException>(() =>
            CalibrationHistoryAnalysis.Compare(first, second, .05, .05));
    }

    [Fact]
    public void HistoryRejectsChangedRoiOrMeasurementMethod()
    {
        var first = Snapshot("v1", 1, 0);
        var second = Snapshot("v2", 2, .02);
        second.Evidence.ImageRegion!.MaxColumn += 1;
        Assert.Throws<ArgumentException>(() =>
            CalibrationHistoryAnalysis.Compare(first, second, .05, .05));
        second.Evidence.ImageRegion.MaxColumn -= 1;
        var changedMethodEvidence = Evidence("v2");
        changedMethodEvidence.MeasurementMethod = "different fixture";
        var changedMethod = new CalibrationSnapshot(second.RecordedUtc,
            changedMethodEvidence, CalibrationEvidenceAudit.Evaluate(
                Calibration(.02), changedMethodEvidence, .2, .2, .2));
        Assert.Throws<ArgumentException>(() =>
            CalibrationHistoryAnalysis.Compare(first, changedMethod, .05, .05));
    }

    [Fact]
    public void HistoryRejectsOutOfOrderTimestampsAndRepeatedIds()
    {
        var a = Snapshot("v1", 1, 0);
        var b = Snapshot("v2", 2, .02);
        var wrongDate = Snapshot("v3", 1, .03);
        Assert.Throws<ArgumentException>(() =>
            CalibrationHistoryAnalysis.Analyze(new[] { a, b, wrongDate }, .05, .05));
        var sameId = Snapshot("v1", 3, .03);
        Assert.Throws<ArgumentException>(() =>
            CalibrationHistoryAnalysis.Analyze(new[] { a, b, sameId }, .05, .05));
    }
}

using System;
using JLVisionLib.Validation;
using Xunit;

namespace JLVisionLib.Tests;

public sealed class AcceptanceMetricsTests
{
    [Fact]
    public void AffineCalibrationMapsRowAndColumnToWorldAxes()
    {
        // Explicit column -> X, row -> Y in a fixed-camera planar setup.
        var calibration = new PlanarAffineCalibration
        {
            XFromColumn = 0.05,
            XFromRow = 0,
            XOffset = 10,
            YFromColumn = 0,
            YFromRow = -0.05,
            YOffset = 100
        };
        calibration.Validate();
        var point = calibration.Transform(row: 200, column: 40);
        Assert.Equal(12, point.Xmm, 9);
        Assert.Equal(90, point.Ymm, 9);
    }

    [Fact]
    public void SingularOrNonFiniteCalibrationIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new PlanarAffineCalibration().Validate());
        Assert.Throws<ArgumentException>(() => new PlanarAffineCalibration
        {
            XFromColumn = double.NaN,
            YFromRow = 1
        }.Validate());
    }

    [Fact]
    public void AngularErrorUsesShortestPathAcrossBoundary()
    {
        double error = VisionAcceptance.AngleErrorDegrees(179 * Math.PI / 180, -179 * Math.PI / 180);
        Assert.Equal(2, error, 8);
    }

    [Fact]
    public void ConfusionMatrixAndRateGatesCatchFalseNegativesAndFalsePositives()
    {
        FrameMeasurement[] data =
        {
            new(true, true, 4, null, null, null, null, null),
            new(true, false, 5, null, null, null, null, null),
            new(false, false, 3, null, null, null, null, null),
            new(false, true, 4, null, null, null, null, null)
        };
        var report = VisionAcceptance.Analyze(data, new AcceptanceCriteria
        {
            MaxMissRate = 0,
            MaxFalsePositiveRate = 0
        });
        Assert.Equal(1, report.TruePositive);
        Assert.Equal(1, report.TrueNegative);
        Assert.Equal(1, report.FalsePositive);
        Assert.Equal(1, report.FalseNegative);
        Assert.Equal(0.5, report.Recall);
        Assert.Equal(0.5, report.Precision);
        Assert.False(report.Passed);
        Assert.Equal(2, report.Checks.Length);
    }

    [Fact]
    public void MillimeterAcceptanceDoesNotPassWithoutAnnotatedGroundTruth()
    {
        FrameMeasurement[] data = { new(true, true, 1.5, null, null, null, 10, 20) };
        var report = VisionAcceptance.Analyze(data, new AcceptanceCriteria { MaxPositionErrorMm = 0.1 });
        Assert.False(report.Passed);
        Assert.Null(report.Checks[0].Observed);
    }

    [Fact]
    public void PositionAngleAndLatencyGatesApplyToMeasuredValues()
    {
        FrameMeasurement[] frames =
        {
            new(true, true, 0.8, 0.08, 0.6, null, 10, 20),
            new(true, true, 1.0, 0.09, 0.8, null, 10, 20),
            new(false, false, 0.6, null, null, null, null, null)
        };
        var criteria = new AcceptanceCriteria
        {
            MaxPositionErrorMm = 0.1,
            MaxAngleErrorDeg = 1,
            MaxP95MatchMs = 2,
            MaxMissRate = 0,
            MaxFalsePositiveRate = 0
        };
        var report = VisionAcceptance.Analyze(frames, criteria);
        Assert.True(report.Passed);
        Assert.Equal(5, report.Checks.Length);

        criteria.MaxPositionErrorMm = 0.05;
        Assert.False(VisionAcceptance.Analyze(frames, criteria).Passed);
    }

    [Fact]
    public void RepeatabilityIsRadialSampleStandardDeviation()
    {
        FrameMeasurement[] frames =
        {
            new(true, true, 1, 0, 0, "stationary-A", 0, 0),
            new(true, true, 1, 0, 0, "stationary-A", 0.1, 0),
            new(true, true, 1, 0, 0, "stationary-A", 0, 0.1),
            new(false, false, 1, null, null, null, null, null)
        };
        var report = VisionAcceptance.Analyze(frames,
            new AcceptanceCriteria { MaxRepeatabilityStdDevMm = 0.1 });
        Assert.Single(report.RepeatabilityGroups);
        Assert.InRange(report.RepeatabilityGroups[0].StdDevMm, 0.081, 0.083);
        Assert.True(report.Passed);

        Assert.False(VisionAcceptance.Analyze(frames,
            new AcceptanceCriteria { MaxRepeatabilityStdDevMm = 0.05 }).Passed);
    }

    [Fact]
    public void RepeatabilityFailsClosedForInsufficientSamples()
    {
        FrameMeasurement[] frames =
        {
            new(true, true, 1, 0, 0, "a", 1, 2),
            new(false, false, 1, null, null, null, null, null)
        };
        Assert.False(VisionAcceptance.Analyze(frames,
            new AcceptanceCriteria { MaxRepeatabilityStdDevMm = 0.1 }).Passed);
    }

    [Fact]
    public void AggregateGateDoesNotPassIfPopulationIsMissing()
    {
        FrameMeasurement[] frames = { new(true, true, 1, null, null, null, null, null) };
        Assert.False(VisionAcceptance.Analyze(frames,
            new AcceptanceCriteria { MaxFalsePositiveRate = 0 }).Passed);
    }
    [Fact]
    public void MinimumPopulationGateRejectsTinyPerfectDatasets()
    {
        FrameMeasurement[] data =
        {
            new(true, true, 1, null, null, null, null, null),
            new(false, false, 1, null, null, null, null, null)
        };
        var report = VisionAcceptance.Analyze(data, new AcceptanceCriteria
        {
            MinPositiveFrames = 100,
            MinNegativeFrames = 50,
            MaxMissRate = 0,
            MaxFalsePositiveRate = 0
        });
        Assert.Equal(4, report.Checks.Length);
        Assert.False(report.Passed);
        Assert.True(report.Checks.Single(c => c.Criterion == "missRate").Passed);
        Assert.False(report.Checks.Single(c => c.Criterion == "minPositiveFrames").Passed);
    }

    [Fact]
    public void MissedFrameFailsMillimeterAcceptanceEvenWithOtherAnnotatedMatches()
    {
        FrameMeasurement[] data =
        {
            new(true, true, 1, 0.02, 0.2, null, 1, 1),
            new(true, false, 1, null, null, null, null, null)
        };
        var report = VisionAcceptance.Analyze(data, new AcceptanceCriteria
        {
            MaxPositionErrorMm = 0.1,
            MaxAngleErrorDeg = 1
        });
        Assert.False(report.Passed);
        Assert.All(report.Checks, c => Assert.False(c.Passed));
    }

}

using System;
using System.Runtime.InteropServices;
using JLVisionLib;
using Xunit;

namespace JLVisionLib.Tests;

/// <summary>
/// Model lifecycle and matching regression checks on deterministic synthetic grayscale images.
/// These tests are not calibrated measurements of industrial localization accuracy.
/// </summary>
public sealed class ShapeAndNccModelTests
{
    private static bool Supported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        RuntimeInformation.ProcessArchitecture == Architecture.X64;

    [Fact]
    public void NccModelCanTrainLocateAndBeDisposedRepeatedly()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        using var fixture = new SyntheticVisionFixture();
        using var template = new JlImage(fixture.TemplatePath);
        using var scene = new JlImage(fixture.ScenePath);

        for (int i = 0; i < 6; i++)
        {
            using var model = new JlNCCModel(template, 3, -0.10, 0.20, 0.03, "use_polarity");
            model.FindNccModel(scene, -0.10, 0.20, 0.35, 1, 0.5, "true", 0,
                out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
            using (row)
            using (column)
            using (angle)
            using (score)
            {
                Assert.Single(row.DArr);
                Assert.Equal(row.Length, column.Length);
                Assert.Equal(row.Length, angle.Length);
                Assert.Equal(row.Length, score.Length);
                Assert.InRange(row[0].D, SyntheticVisionFixture.TargetTop - 5,
                    SyntheticVisionFixture.TargetTop + SyntheticVisionFixture.TargetHeight + 5);
                Assert.InRange(column[0].D, SyntheticVisionFixture.TargetLeft - 5,
                    SyntheticVisionFixture.TargetLeft + SyntheticVisionFixture.TargetWidth + 5);
                Assert.InRange(score[0].D, 0.35, 1.0);
            }
        }
    }

    [Fact]
    public void ShapeModelCanTrainLocateAndBeDisposedRepeatedly()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        using var fixture = new SyntheticVisionFixture();
        using var template = new JlImage(fixture.TemplatePath);
        using var scene = new JlImage(fixture.ScenePath);

        for (int i = 0; i < 6; i++)
        {
            using var model = new JlShapeModel(template, 3, -0.10, 0.20, 0.03,
                "auto", "use_polarity", 20, 10);
            model.FindShapeModel(scene, -0.10, 0.20, 0.35, 1, 0.5, "least_squares", 0, 0.8,
                out JlTuple row, out JlTuple column, out JlTuple angle, out JlTuple score);
            using (row)
            using (column)
            using (angle)
            using (score)
            {
                Assert.Single(row.DArr);
                Assert.Equal(row.Length, column.Length);
                Assert.Equal(row.Length, angle.Length);
                Assert.Equal(row.Length, score.Length);
                Assert.InRange(row[0].D, SyntheticVisionFixture.TargetTop - 5,
                    SyntheticVisionFixture.TargetTop + SyntheticVisionFixture.TargetHeight + 5);
                Assert.InRange(column[0].D, SyntheticVisionFixture.TargetLeft - 5,
                    SyntheticVisionFixture.TargetLeft + SyntheticVisionFixture.TargetWidth + 5);
                Assert.InRange(score[0].D, 0.35, 1.0);
            }
        }
    }
}

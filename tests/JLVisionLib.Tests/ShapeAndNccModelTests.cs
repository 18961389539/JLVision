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
    [Fact]
    public void TrainedModelsRemainRepeatableAcrossSuccessiveFrames()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        using var fixture = new SyntheticVisionFixture();
        using var template = new JlImage(fixture.TemplatePath);
        using var scene = new JlImage(fixture.ScenePath);
        using var ncc = new JlNCCModel(template, 3, -0.10, 0.20, 0.03, "use_polarity");
        using var shape = new JlShapeModel(template, 3, -0.10, 0.20, 0.03,
            "auto", "use_polarity", 20, 10);

        (double row, double col, double score)? nccReference = null;
        (double row, double col, double score)? shapeReference = null;

        for (int i = 0; i < 20; i++)
        {
            ncc.FindNccModel(scene, -0.10, 0.20, 0.35, 1, 0.5, "true", 0,
                out JlTuple nccRows, out JlTuple nccColumns, out JlTuple nccAngles, out JlTuple nccScores);
            using (nccRows)
            using (nccColumns)
            using (nccAngles)
            using (nccScores)
            {
                Assert.Single(nccRows.DArr);
                var result = (nccRows[0].D, nccColumns[0].D, nccScores[0].D);
                if (nccReference.HasValue)
                {
                    Assert.InRange(Math.Abs(result.Item1 - nccReference.Value.row), 0, 0.25);
                    Assert.InRange(Math.Abs(result.Item2 - nccReference.Value.col), 0, 0.25);
                    Assert.InRange(Math.Abs(result.Item3 - nccReference.Value.score), 0, 0.01);
                }
                nccReference = result;
            }

            shape.FindShapeModel(scene, -0.10, 0.20, 0.35, 1, 0.5,
                "least_squares", 0, 0.8,
                out JlTuple shapeRows, out JlTuple shapeColumns, out JlTuple shapeAngles, out JlTuple shapeScores);
            using (shapeRows)
            using (shapeColumns)
            using (shapeAngles)
            using (shapeScores)
            {
                Assert.Single(shapeRows.DArr);
                var result = (shapeRows[0].D, shapeColumns[0].D, shapeScores[0].D);
                if (shapeReference.HasValue)
                {
                    Assert.InRange(Math.Abs(result.Item1 - shapeReference.Value.row), 0, 0.25);
                    Assert.InRange(Math.Abs(result.Item2 - shapeReference.Value.col), 0, 0.25);
                    Assert.InRange(Math.Abs(result.Item3 - shapeReference.Value.score), 0, 0.01);
                }
                shapeReference = result;
            }
        }
    }

    [Fact]
    public void ModelsDoNotFindHighConfidenceMatchesInNegativeScene()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        using var fixture = new SyntheticVisionFixture();
        using var template = new JlImage(fixture.TemplatePath);
        using var negative = new JlImage(fixture.NegativeScenePath);
        using var ncc = new JlNCCModel(template, 3, -0.10, 0.20, 0.03, "use_polarity");
        using var shape = new JlShapeModel(template, 3, -0.10, 0.20, 0.03,
            "auto", "use_polarity", 20, 10);

        ncc.FindNccModel(negative, -0.10, 0.20, 0.80, 1, 0.5, "true", 0,
            out JlTuple nccRows, out JlTuple nccColumns, out JlTuple nccAngles, out JlTuple nccScores);
        using (nccRows)
        using (nccColumns)
        using (nccAngles)
        using (nccScores)
            Assert.Empty(nccRows.DArr);

        shape.FindShapeModel(negative, -0.10, 0.20, 0.80, 1, 0.5, "least_squares", 0, 0.8,
            out JlTuple shapeRows, out JlTuple shapeColumns, out JlTuple shapeAngles, out JlTuple shapeScores);
        using (shapeRows)
        using (shapeColumns)
        using (shapeAngles)
        using (shapeScores)
            Assert.Empty(shapeRows.DArr);
    }

}

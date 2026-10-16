using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using JLVisionLib;
using Xunit;

namespace JLVisionLib.Tests;

/// <summary>
/// Focused CI-sized tests. These deliberately do not share mutable native wrappers across threads.
/// Long-running memory and latency measurement lives in tools/JLVision.Stress.
/// </summary>
public sealed class NativeReliabilityTests
{
    private static bool Supported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
        RuntimeInformation.ProcessArchitecture == Architecture.X64;

    [Fact]
    public void RepeatedImageRegionAndTupleWorkloadRemainsUsable()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        for (int i = 0; i < 96; i++)
        {
            RunOneIndependentWorkload();
        }
    }

    [Fact]
    public async Task IndependentWorkloadsCanRunConcurrently()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        // No JlImage/JlRegion/JlTuple instance crosses a worker boundary.
        const int workers = 4;
        const int operationsPerWorker = 16;
        var tasks = new Task[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            tasks[worker] = Task.Run(() =>
            {
                for (int i = 0; i < operationsPerWorker; i++)
                    RunOneIndependentWorkload();
            });
        }
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void NativeOperatorFailuresDoNotPoisonSubsequentOperations()
    {
        if (!Supported) return;
        JLVisionRuntime.Initialize();

        for (int i = 0; i < 48; i++)
        {
            using var input = new JlTuple(10, 20);
            using var invalidIndex = new JlTuple(-1);
            Assert.ThrowsAny<Exception>(() =>
            {
                using var invalid = input.TupleSelect(invalidIndex);
            });

            using var valid = input.TupleAbs();
            Assert.Equal(10, valid[0].I);
            Assert.Equal(20, valid[1].I);
        }
    }

    private static void RunOneIndependentWorkload()
    {
        using var image = new JlImage(JlImageType.Byte, 96, 64);
        image.GetImageSize(out int width, out int height);
        Assert.Equal(96, width);
        Assert.Equal(64, height);

        using var region = image.Threshold(0.0, 255.0);
        Assert.True(region.IsInitialized());

        using var rectangle = new JlRegion(4.0, 5.0, 24.0, 35.0);
        int area = rectangle.AreaCenter(out double row, out double col);
        Assert.True(area > 0);
        Assert.InRange(row, 3.0, 25.0);
        Assert.InRange(col, 4.0, 36.0);

        using var input = new JlTuple(-4, 9);
        using var output = input.TupleAbs();
        Assert.Equal(4, output[0].I);
        Assert.Equal(9, output[1].I);
    }
}

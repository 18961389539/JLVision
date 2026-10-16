using System.Runtime.InteropServices;
using JLVisionLib;
using Xunit;

namespace JLVisionLib.Tests;

/// <summary>Exercise the bundled native runtime on supported Windows x64 runners.</summary>
public sealed class NativeSmokeTests
{
    [Fact]
    public void WindowsX64CanInitializeAndReleaseImages()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return;
        }

        JlRuntimeDiagnostics diagnostics = JLVisionRuntime.Initialize();
        Assert.Equal(JlRuntimeStatus.Ready, diagnostics.Status);
        Assert.True(File.Exists(diagnostics.NativeLibraryPath));

        // Repeated native allocations catch packaging/entry-point mistakes and
        // basic lifetime regressions without assuming algorithm result values.
        for (int i = 0; i < 32; i++)
        {
            using var image = new JlImage(JlImageType.Byte, 64, 48);
            Assert.True(image.IsInitialized());
        }
    }
    [Fact]
    public void InvalidTupleOperationCanBeRepeatedWithoutBreakingNextCall()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return;
        }

        JLVisionRuntime.Initialize();
        using var source = new JlTuple(11, 22);
        using var invalidIndex = new JlTuple(-1);
        for (int i = 0; i < 24; i++)
        {
            Assert.ThrowsAny<Exception>(() =>
            {
                using var unexpected = source.TupleSelect(invalidIndex);
            });

            using var result = source.TupleAbs();
            Assert.Equal(2, result.Length);
            Assert.Equal(11, result[0].I);
        }
    }

    [Fact]
    public void ImageAndRegionGeneratedWrappersSurviveRepeatedNativeCalls()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return;
        }

        JLVisionRuntime.Initialize();
        for (int i = 0; i < 16; i++)
        {
            using var image = new JlImage(JlImageType.Byte, 64, 48);
            image.GetImageSize(out int width, out int height);
            Assert.Equal(64, width);
            Assert.Equal(48, height);

            using var threshold = image.Threshold(0.0, 255.0);
            Assert.True(threshold.IsInitialized());

            using var rectangle = new JlRegion(5.0, 6.0, 15.0, 16.0);
            int area = rectangle.AreaCenter(out double row, out double column);
            Assert.True(area > 0);
            Assert.InRange(row, 4.0, 16.0);
            Assert.InRange(column, 5.0, 17.0);
        }
    }

    [Fact]
    public void OperatorFacadeCanBeCalledRepeatedlyWithoutLeakingCallState()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return;
        }

        JLVisionRuntime.Initialize();
        using var input = new JlTuple(-12, 14);
        for (int i = 0; i < 24; i++)
        {
            JlOperatorSet.TupleAbs(input, out JlTuple result);
            using (result)
            {
                Assert.Equal(12, result[0].I);
                Assert.Equal(14, result[1].I);
            }
        }
    }

}

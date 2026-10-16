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

}

using System.Runtime.InteropServices;
using JLVisionLib;

if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
    throw new PlatformNotSupportedException("The packaged JLVision native runtime requires Windows x64.");

var runtime = JLVisionRuntime.Initialize();
if (!runtime.IsReady || !File.Exists(runtime.NativeLibraryPath))
    throw new InvalidOperationException("The NuGet package did not install a loadable native DLL.");

using var image = new JlImage(JlImageType.Byte, 128, 96);
image.GetImageSize(out int width, out int height);
if (width != 128 || height != 96)
    throw new InvalidOperationException("Packaged image API produced incorrect dimensions.");

using var region = image.Threshold(0.0, 255.0);
if (!region.IsInitialized())
    throw new InvalidOperationException("Packaged region API did not return an initialized region.");

using var values = new JlTuple(-9, 11);
using var result = values.TupleAbs();
if (result[0].I != 9 || result[1].I != 11)
    throw new InvalidOperationException("Packaged tuple API returned incorrect results.");

Console.WriteLine("NuGet package native integration smoke test passed.");
Console.WriteLine("Native DLL: " + runtime.NativeLibraryPath);

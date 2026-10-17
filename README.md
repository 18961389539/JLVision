# JLVisionLib

JLVisionLib is a .NET Standard 2.0 wrapper for the JLVision image processing runtime.

## Runtime support

The NuGet package currently ships the `JLVisionCore.dll` native runtime for Windows x64. The package build targets copy that native asset to the application output directory for Windows x64 consumers.

```csharp
using JLVisionLib;

JlRuntimeDiagnostics diagnostics = JLVisionRuntime.Initialize();
using JlImage image = new JlImage(JlImageType.Byte, 640, 480);
```

Call `JLVisionRuntime.TryInitialize(out JlRuntimeDiagnostics diagnostics)` during application startup when a non-throwing runtime check is preferred.

The public string overloads remain available for compatibility. Typed option overloads are provided for common image, pose, model, and transformation operations.


## Managed 1D and 2D code recognition adapter

For functional Code128, Code39, EAN, QR and DataMatrix decoding without
guessing unverified native operator indices, build the **optional**
`JLVisionLib.Recognition` project (ZXing.Net 0.16.11).

This restores the high-level `JlBarCode` and `JlDataCode2D` API entry
points under `JLVisionLib.Recognition`, including Gray8, ROI, and `JlImage` inputs.
A **separate optional** `JLVisionLib.Ocr.Tesseract` assembly adds functional
`JlOcr` recognition with an externally provisioned Tesseract language model.
Neither adapter restores the original native DLL recognition procedures.
See [recognition adapter](docs/recognition-adapter.md) and
[recognition native recovery plan](docs/recognition-recovery-plan.md).

## Native reliability and performance validation

The Windows x64 CI workflow checks generated native procedure lifetime scopes, runs managed/native tests, compares serial and independent-worker throughput, and validates the **built Release NuGet package** in a separate consumer process.

For multi-hour, single-process stress testing and interpretation of JSON metrics, see [native reliability testing](docs/native-reliability.md). For example:

```powershell
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --duration-seconds 3600 --workers 4 --batches 60 --width 640 --height 480 --output artifacts/stress-one-hour.json
```

The tests demonstrate independent-worker operation only; concurrent access to one mutable image, region or tuple instance has not been certified thread-safe.

For offline Shape/NCC matching against labeled production images (positive/negative frames, optional position/angle tolerances), use [the labeled dataset benchmark](docs/native-reliability.md#labeled-production-image-validation-positive-and-negative-frames) and its [example manifest](tools/JLVision.DatasetBenchmark/example.dataset.json). Keep customer images outside the public repository.


For industrial acceptance with independently measured pixel-to-mm calibration, minimum positive/negative sample counts, angle, detection-rate and repeatability gates, see [industrial vision acceptance protocol](docs/industrial-vision-acceptance.md). The example coefficients and acceptance values are illustrative only and are not real calibration results.

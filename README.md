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

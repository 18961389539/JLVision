# JLVision Windows x64 reliability and stress testing

This document describes repeatable software-only native interop testing. No industrial camera, robot or PLC is required.

## Execution prerequisites

- Windows x64 and .NET 8 SDK or later.
- The repo's `Libs/JLVisionCore.dll` and its required native dependencies are available. The test/stress projects copy the runtime to their output folder.
- The JLVision native runtime must initialize successfully before collecting results.

## CI smoke tests

```powershell
dotnet restore JLVisionLib.slnx
dotnet test JLVisionLib.slnx --configuration Debug
```

The `NativeReliabilityTests` cases deliberately create independent native image, region and tuple instances for every worker. They test concurrent *independent* workloads, not concurrent mutation of the same object. An invalid tuple-index operation is followed by a valid one to check recovery.

The Windows x64 GitHub Actions pipeline additionally executes a **bounded** 400-operation stress baseline and publishes `jlv-stress-report` as a JSON artifact. It records native-process behavior without making machine-dependent memory or latency values hard pass/fail thresholds.

## Longer local stress runs

```powershell
# A modest initial baseline on the same PC that will run the production software.
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --iterations 3000 --workers 1 --batches 6 --width 640 --height 480 --output artifacts/stress-serial.json

# Independent worker throughput and stability; no mutable object is shared.
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --iterations 12000 --workers 4 --batches 12 --width 640 --height 480 --output artifacts/stress-parallel.json

# Larger overnight runs can increase iterations up to 200000.
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --iterations 80000 --workers 4 --batches 20 --width 640 --height 480 --output artifacts/stress-long.json
```

The runner exits nonzero on incorrect image dimensions, failed native image/region operations, unexpected native exceptions, incorrect tuple results, or incomplete operations. It also periodically triggers an expected operator error followed by a valid operation to check recovery.

## JSON report fields

- `configuration`: operation count, worker count, image dimensions and batch count.
- `results.throughputOperationsPerSecond`: *whole measurement period* throughput (includes inter-batch memory samples).
- `results.operationLatencyMs`: per-operation P50, P95, P99, maximum; does **not** include inter-batch pauses.
- `memorySamples`: after-warmup baseline and per-batch `PrivateBytes`, `WorkingSetBytes`, and managed heap bytes after forced GC.
- `results.privateByteDeltaAfterGc`: last sample's private bytes minus warmup baseline.
- `errors`: the first failures with operation indexes.

Because native allocators can retain pools and memory managers behave differently, a nonzero private-byte delta is **not by itself a leak**. Treat monotonically rising post-GC memory over repeated, comparable runs as an investigation signal. Compare runs on the same machine, image dimensions, worker count, warmup settings, and runtime version.

## Before approving production release

1. Establish a repeatable serial baseline, then test independent concurrency at 2/4/8 workers.
2. Collect at least three reports per configuration; inspect memory after multiple batches and P95/P99 outliers.
3. Check for native memory corruption separately with appropriate Windows diagnostics and a controlled debug environment.
4. Do not share or dispose mutable `JlImage`, `JlRegion`, or `JlTuple` instances concurrently until their ownership/locking contracts are explicitly documented and tested.
5. Run longer shape matching and image-heavy workflows representative of production loads. This harness is only a deterministic smoke/stability workload, not an algorithm-accuracy benchmark.
6. Review native DLL licensing, dependency versions and the 256 MiB serialization input cap.

The generated CI stress artifact is intended as a baseline; thresholds and scheduled long-duration execution should be configured only after observing real workload variability.

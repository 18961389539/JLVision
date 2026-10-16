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

The Windows x64 GitHub Actions pipeline executes **two bounded, comparable 3,000-operation workloads** (one worker and four independent workers, both 320×240), plus a 2-second timed-mode sanity check. It publishes `jlv-stress-report` containing separate `jlv-stress-serial.json`, `jlv-stress-parallel.json` and `jlv-stress-timed.json` artifacts. Compare operation throughput and P95/P99 only on the **same runner** and use repeated runs for reliable trends. Native-process memory and latency values remain diagnostic rather than machine-dependent pass/fail thresholds.

## Longer local stress runs

```powershell
# A modest initial baseline on the same PC that will run the production software.
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --iterations 3000 --workers 1 --batches 6 --width 640 --height 480 --output artifacts/stress-serial.json

# Independent worker throughput and stability; no mutable object is shared.
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --iterations 12000 --workers 4 --batches 12 --width 640 --height 480 --output artifacts/stress-parallel.json

# A true one-hour single-process soak, with periodic after-GC memory samples.
# A maximum of 100000 recent operation latencies are retained in timed mode.
dotnet run --project tools/JLVision.Stress/JLVision.Stress.csproj --configuration Debug -- --duration-seconds 3600 --workers 4 --batches 60 --width 640 --height 480 --output artifacts/stress-one-hour.json
```

The runner exits nonzero on incorrect image dimensions, failed native image/region operations, unexpected native exceptions, incorrect tuple results, or incomplete operations. It also periodically triggers an expected operator error followed by a valid operation to check recovery. In timed mode (`--duration-seconds`) it runs repeatedly **within one process** for the requested wall-clock duration, samples memory between batches and retains only the most recent 100000 operation latencies, so it remains practical for hours. Timed mode takes precedence over `--iterations` if both are provided. The mode supports up to 24 hours per invocation.

## JSON report fields

- `configuration`: operation count, worker count, image dimensions and batch count.
- `results.throughputOperationsPerSecond`: *whole measurement period* throughput (includes inter-batch memory samples).
- `results.operationLatencyMs`: per-operation P50, P95, P99, maximum; does **not** include inter-batch pauses. In timed mode these are computed from at most the **last 100000 operations**, not every operation across the whole soak.
- `memorySamples`: after-warmup baseline and per-batch `PrivateBytes`, `WorkingSetBytes`, and managed heap bytes after forced GC.
- `results.privateByteDeltaAfterGc`: last sample's private bytes minus warmup baseline.
- `errors`: the first failures with operation indexes.

Because native allocators can retain pools and memory managers behave differently, a nonzero private-byte delta is **not by itself a leak**. Treat monotonically rising post-GC memory over repeated, comparable runs as an investigation signal. Compare runs on the same machine, image dimensions, worker count, warmup settings, and runtime version.

## Validating the distributed Release NuGet package

The CI pipeline also restores a **separate consumer application** from the freshly generated `artifacts/JLVisionLib.1.0.0.nupkg`, rather than referencing the repository project directly. This checks that the public managed APIs and the bundled `JLVisionCore.dll` work after packaging and Release obfuscation:

```powershell
dotnet pack JLVisionLib.csproj --configuration Release --output artifacts
dotnet restore tests/JLVisionLib.PackageSmoke/JLVisionLib.PackageSmoke.csproj --source ./artifacts
dotnet run --project tests/JLVisionLib.PackageSmoke/JLVisionLib.PackageSmoke.csproj --configuration Release --no-restore
```

## Shape/NCC model matching baseline

CI generates a deterministic 8-bit grayscale BMP target and larger scene, then verifies each native model can be trained, locate the inserted target, and be disposed repeatedly. This provides **regression coverage**, not a claim that either matcher achieves a stated industrial accuracy.

Run the standalone benchmark to separately measure model training and matching time, report detections, score trends, basic position offset (synthetic fixture only) and after-GC memory:

```powershell
dotnet run --project tools/JLVision.ModelBenchmark/JLVision.ModelBenchmark.csproj --configuration Debug -- --algorithm both --iterations 100 --output artifacts/models-synthetic.json
```

To benchmark against **your own real images** without adding them to the public repository, supply matching local paths. Use a cropped, representative template and a scene containing the same part:

```powershell
dotnet run --project tools/JLVision.ModelBenchmark/JLVision.ModelBenchmark.csproj --configuration Debug -- --algorithm both --iterations 100 --template "C:\\vision-data\\template.bmp" --scene "C:\\vision-data\\scene.bmp" --output artifacts/models-real.json
```

The same command supports `--algorithm ncc` or `--algorithm shape`. Both are timed independently. Each iteration **recreates and disposes the model**, deliberately stressing the model lifecycle; it is **not** representative of a production line that trains once and only matches on each frame. For sustained match-only performance, use a separate benchmark once trained model reuse and its ownership contract are established.

For real image paths, matching must be checked by a commissioning engineer against independently measured row/column/angle ground truth. This tool does not report calibrated mm error; the synthetic offset is a pixel-distance diagnostic only. Model origin/pose convention can differ from the geometric image center. Failure to find a target produces a nonzero exit code; no positive detection is silently treated as a good localization result.

The workflow publishes model measurements under the `jlv-model-benchmark` artifact. For field-quality acceptance, prepare controlled datasets covering part changes, lighting drift, blur, rotation, occlusion and negative examples, then specify tolerances independently of these smoke tests.

## Labeled production-image validation (positive and negative frames)

For model **reuse across frames**, use `JLVision.DatasetBenchmark` rather than the training-lifecycle benchmark. By default it generates a positive and a blank negative BMP fixture, trains each model once, then checks both frames.

```powershell
dotnet run --project tools/JLVision.DatasetBenchmark/JLVision.DatasetBenchmark.csproj --configuration Debug -- --algorithm both --output artifacts/model-dataset.json
```

To assess **private, real image samples**, copy `tools/JLVision.DatasetBenchmark/example.dataset.json` to a local folder, replace the example paths and annotation coordinates, and execute:

```powershell
dotnet run --project tools/JLVision.DatasetBenchmark/JLVision.DatasetBenchmark.csproj --configuration Debug -- --manifest "C:\\vision-dataset\\dataset.json" --algorithm both --output artifacts/model-dataset-real.json
```

The manifest contains:
- `template`: image for model training; resolved relative to the manifest location unless absolute
- `minScore`: the minimum model matching score (greater than 0, at most 1)
- `frames[].file`: positive and negative scene images
- `frames[].expectedPresent`: **required explicit boolean** target-presence label; controls missed/false-positive classification
- Optional paired `expectedRow` and `expectedColumn`: **model reference-point** ground truth in image pixels, not automatically the bounding-box center
- Optional `tolerancePixels`: allowed Euclidean position error in pixels (default 5)
- Optional paired `expectedAngleRad` and `toleranceAngleRad`: target orientation in radians and angular error allowance (default 0.10 rad); angular error is wrapped across the ±π boundary

Output includes per-frame detection, score, angle, latency and annotated position error, plus per-algorithm TP/TN/FP/FN, localization failures and P50/P95 search times. The process exits nonzero when an annotation fails. This is an **offline software-only** metric. Calibrated millimeter accuracy requires independently established image-to-world geometry, operator-verified annotations and an actual production-image dataset. Do not commit confidential production images to the public repository.

CI checks only the small synthetic positive/negative set and publishes a `jlv-model-dataset` artifact; it cannot certify performance on unprovided customer data.

## Before approving production release

1. Establish a repeatable serial baseline, then test independent concurrency at 2/4/8 workers.
2. Collect at least three reports per configuration; inspect memory after multiple batches and P95/P99 outliers.
3. Check for native memory corruption separately with appropriate Windows diagnostics and a controlled debug environment.
4. Do not share or dispose mutable `JlImage`, `JlRegion`, or `JlTuple` instances concurrently until their ownership/locking contracts are explicitly documented and tested.
5. Run longer shape matching and image-heavy workflows representative of production loads. This harness is only a deterministic smoke/stability workload, not an algorithm-accuracy benchmark.
6. Review native DLL licensing, dependency versions and the 256 MiB serialization input cap.

The generated CI stress artifact is intended as a baseline; thresholds and scheduled long-duration execution should be configured only after observing real workload variability.

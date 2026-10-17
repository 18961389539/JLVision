using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JLVisionLib;

internal static class Program
{
    private sealed record MemorySample(
        string Phase, long Completed, long PrivateBytes, long WorkingSetBytes, long ManagedHeapBytes);

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        int iterations = ReadInt(args, "--iterations", 2_000, 1, 200_000);
        int workers = ReadInt(args, "--workers", 4, 1, 16);
        int width = ReadInt(args, "--width", 160, 16, 4_096);
        int height = ReadInt(args, "--height", 120, 16, 4_096);
        int batches = ReadInt(args, "--batches", 5, 1, 100);
        int durationSeconds = ReadInt(args, "--duration-seconds", 0, 0, 86_400);
        string output = ReadString(args, "--output", "artifacts/jlv-stress.json");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("JLVision native stress requires Windows x64.");
        }

        JlRuntimeDiagnostics runtime = JLVisionRuntime.Initialize();
        Console.WriteLine("Native runtime: " + runtime.NativeLibraryPath);
        Console.WriteLine($"Iterations={iterations}, Workers={workers}, Image={width}x{height}, Batches={batches}");

        if (durationSeconds > 0)
            return RunTimed(durationSeconds, workers, width, height, batches, output, runtime);

        // Warm up JIT, native initialization, caches and thread pool before measuring.
        for (int i = 0; i < Math.Max(16, workers * 4); i++)
            RunWorkload(width, height, includeExpectedFailure: i % 7 == 0);

        using Process process = Process.GetCurrentProcess();
        List<MemorySample> memory = new();
        TakeMemorySample(process, memory, "after-warmup", 0);

        long[] durations = new long[iterations];
        int completed = 0;
        ConcurrentQueue<string> failures = new();
        ParallelOptions parallelism = new() { MaxDegreeOfParallelism = workers };
        Stopwatch total = Stopwatch.StartNew();

        for (int batch = 0; batch < batches; batch++)
        {
            int start = (int)((long)iterations * batch / batches);
            int end = (int)((long)iterations * (batch + 1) / batches);
            Parallel.For(start, end, parallelism, (i, loopState) =>
            {
                if (!failures.IsEmpty)
                {
                    loopState.Stop();
                    return;
                }
                long started = Stopwatch.GetTimestamp();
                try
                {
                    RunWorkload(width, height, includeExpectedFailure: i % 17 == 0);
                    durations[i] = Stopwatch.GetTimestamp() - started;
                    Interlocked.Increment(ref completed);
                }
                catch (Exception e)
                {
                    failures.Enqueue($"Operation {i}: {e}");
                    loopState.Stop();
                }
            });

            TakeMemorySample(process, memory, $"batch-{batch + 1}", completed);
            Console.WriteLine($"Batch {batch + 1}/{batches}: completed={completed}, privateBytes={memory[^1].PrivateBytes}");

            if (!failures.IsEmpty)
                break;
        }
        total.Stop();

        long[] validTicks = durations.Where(x => x > 0).OrderBy(x => x).ToArray();
        double Millis(long ticks) => ticks * 1_000.0 / Stopwatch.Frequency;
        double Percentile(double proportion) =>
            validTicks.Length == 0
                ? 0
                : Millis(validTicks[(int)Math.Ceiling((validTicks.Length - 1) * proportion)]);

        var report = new
        {
            schemaVersion = 1,
            createdUtc = DateTimeOffset.UtcNow,
            operatingSystem = RuntimeInformation.OSDescription,
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            logicalProcessors = Environment.ProcessorCount,
            nativeLibrary = Path.GetFileName(runtime.NativeLibraryPath),
            configuration = new { iterations, workers, width, height, batches },
            results = new
            {
                completed,
                failed = failures.Count,
                durationSeconds = total.Elapsed.TotalSeconds,
                throughputOperationsPerSecond = total.Elapsed.TotalSeconds > 0
                    ? completed / total.Elapsed.TotalSeconds : 0,
                operationLatencyMs = new
                {
                    p50 = Percentile(0.50),
                    p95 = Percentile(0.95),
                    p99 = Percentile(0.99),
                    max = validTicks.Length == 0 ? 0 : Millis(validTicks[^1])
                },
                privateByteDeltaAfterGc = memory[^1].PrivateBytes - memory[0].PrivateBytes
            },
            memorySamples = memory,
            errors = failures.ToArray()
        };

        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        Console.WriteLine($"Report: {fullPath}");
        Console.WriteLine($"Throughput: {report.results.throughputOperationsPerSecond:F1} ops/s; " +
                          $"p50={report.results.operationLatencyMs.p50:F2} ms; " +
                          $"p95={report.results.operationLatencyMs.p95:F2} ms; " +
                          $"p99={report.results.operationLatencyMs.p99:F2} ms");
        Console.WriteLine($"Private bytes after-GC delta: {report.results.privateByteDeltaAfterGc:N0} bytes");
        // Memory delta is diagnostic. Native allocators cache memory; no universal leak threshold.
        return failures.IsEmpty && completed == iterations ? 0 : 2;
    }

    private static int RunTimed(
        int durationSeconds, int workers, int width, int height, int batches,
        string output, JlRuntimeDiagnostics runtime)
    {
        Console.WriteLine($"Time-based soak: {durationSeconds} seconds, workers={workers}, image={width}x{height}");
        for (int i = 0; i < Math.Max(16, workers * 4); i++)
            RunWorkload(width, height, includeExpectedFailure: i % 7 == 0);

        using Process process = Process.GetCurrentProcess();
        List<MemorySample> memory = new();
        TakeMemorySample(process, memory, "after-warmup", 0);

        // A bounded circular buffer keeps one-hour/overnight soak runs from
        // accumulating millions of latency samples in managed memory.
        const int MaxLatencySamples = 100_000;
        long[] lastLatencies = new long[MaxLatencySamples];
        long completed = 0;
        ConcurrentQueue<string> failures = new();
        ParallelOptions parallelism = new() { MaxDegreeOfParallelism = workers };
        Stopwatch total = Stopwatch.StartNew();
        double intervalSeconds = (double)durationSeconds / batches;
        double nextSnapshotSeconds = intervalSeconds;
        int sampleNumber = 0;

        while (total.Elapsed.TotalSeconds < durationSeconds && failures.IsEmpty)
        {
            // Check the elapsed time between bounded parallel blocks so the
            // deadline is respected without sharing mutable native objects.
            Parallel.For(0, Math.Max(128, workers * 64), parallelism, (_, loopState) =>
            {
                if (!failures.IsEmpty)
                {
                    loopState.Stop();
                    return;
                }

                long started = Stopwatch.GetTimestamp();
                try
                {
                    long sequence = Volatile.Read(ref completed);
                    RunWorkload(width, height, includeExpectedFailure: sequence % 17 == 0);
                    long elapsedTicks = Stopwatch.GetTimestamp() - started;
                    long sampleIndex = Interlocked.Increment(ref completed) - 1;
                    lastLatencies[(int)(sampleIndex % MaxLatencySamples)] = elapsedTicks;
                }
                catch (Exception e)
                {
                    failures.Enqueue(e.ToString());
                    loopState.Stop();
                }
            });

            if (total.Elapsed.TotalSeconds >= nextSnapshotSeconds)
            {
                TakeMemorySample(process, memory, $"elapsed-{++sampleNumber}", completed);
                Console.WriteLine($"Elapsed {total.Elapsed.TotalSeconds:F1}s: completed={completed}, privateBytes={memory[^1].PrivateBytes}");
                nextSnapshotSeconds += intervalSeconds;
            }
        }
        total.Stop();
        TakeMemorySample(process, memory, "end", completed);

        long[] sorted = lastLatencies.Take((int)Math.Min(completed, MaxLatencySamples))
            .Where(t => t > 0).OrderBy(t => t).ToArray();
        double Millis(long ticks) => ticks * 1_000.0 / Stopwatch.Frequency;
        double Percentile(double portion) => sorted.Length == 0
            ? 0 : Millis(sorted[(int)Math.Ceiling((sorted.Length - 1) * portion)]);

        var report = new
        {
            schemaVersion = 1,
            createdUtc = DateTimeOffset.UtcNow,
            operatingSystem = RuntimeInformation.OSDescription,
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            logicalProcessors = Environment.ProcessorCount,
            nativeLibrary = Path.GetFileName(runtime.NativeLibraryPath),
            configuration = new { mode = "timed", durationSeconds, workers, width, height, batches },
            results = new
            {
                completed,
                failed = failures.Count,
                durationSecondsActual = total.Elapsed.TotalSeconds,
                throughputOperationsPerSecond = total.Elapsed.TotalSeconds > 0
                    ? completed / total.Elapsed.TotalSeconds : 0,
                latencySampleCount = sorted.Length,
                operationLatencyMs = new
                {
                    p50 = Percentile(0.50),
                    p95 = Percentile(0.95),
                    p99 = Percentile(0.99),
                    max = sorted.Length == 0 ? 0 : Millis(sorted[^1])
                },
                privateByteDeltaAfterGc = memory[^1].PrivateBytes - memory[0].PrivateBytes
            },
            memorySamples = memory,
            errors = failures.ToArray()
        };

        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        Console.WriteLine($"Report: {fullPath}");
        Console.WriteLine($"Timed throughput: {report.results.throughputOperationsPerSecond:F1} ops/s; " +
                          $"p95={report.results.operationLatencyMs.p95:F2} ms; " +
                          $"p99={report.results.operationLatencyMs.p99:F2} ms");
        Console.WriteLine($"Timed memory delta: {report.results.privateByteDeltaAfterGc:N0} bytes");
        return failures.IsEmpty && completed > 0 ? 0 : 2;
    }

    private static void RunWorkload(int width, int height, bool includeExpectedFailure)
    {
        using var image = new JlImage(JlImageType.Byte, width, height);
        image.GetImageSize(out int actualWidth, out int actualHeight);
        if (actualWidth != width || actualHeight != height)
            throw new InvalidOperationException("Native image dimensions differ from requested dimensions.");

        using var threshold = image.Threshold(0.0, 255.0);
        if (!threshold.IsInitialized())
            throw new InvalidOperationException("Threshold output is not initialized.");

        using var rectangle = new JlRegion(5.0, 6.0, 40.0, 46.0);
        if (rectangle.AreaCenter(out double row, out double col) <= 0 ||
            double.IsNaN(row) || double.IsNaN(col))
            throw new InvalidOperationException("Native region geometry is invalid.");

        using var values = new JlTuple(-12, 14);
        if (includeExpectedFailure)
        {
            using var invalidIndex = new JlTuple(-1);
            bool failedAsExpected = false;
            try
            {
                using var invalid = values.TupleSelect(invalidIndex);
            }
            catch (JlOperatorException)
            {
                failedAsExpected = true;
            }
            if (!failedAsExpected)
                throw new InvalidOperationException("Invalid tuple index unexpectedly succeeded.");
        }

        using var result = values.TupleAbs();
        if (result[0].I != 12 || result[1].I != 14)
            throw new InvalidOperationException("Native tuple absolute-value result was incorrect.");
    }

    private static void TakeMemorySample(Process process, List<MemorySample> samples, string phase, long completed)
    {
        // The measured steady-state excludes known, collectible managed allocations.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        process.Refresh();
        samples.Add(new MemorySample(phase, completed,
            process.PrivateMemorySize64, process.WorkingSet64, GC.GetTotalMemory(false)));
    }

    private static int ReadInt(string[] args, string name, int fallback, int min, int max)
    {
        string text = ReadString(args, name, fallback.ToString());
        if (!int.TryParse(text, out int number) || number < min || number > max)
            throw new ArgumentException($"{name} must be an integer between {min} and {max}.");
        return number;
    }

    private static string ReadString(string[] args, string name, string fallback)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name)
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value for {name}.");
                return args[i + 1];
            }
        }
        return fallback;
    }
}

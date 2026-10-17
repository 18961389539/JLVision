using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using JLVisionLib;
using JLVisionLib.Tests;

internal static class Program
{
    private sealed record MemoryReading(string Phase, int Completed, long PrivateBytes, long ManagedHeapBytes);
    private sealed record Measurement(
        string Algorithm, double TrainMs, double MatchMs,
        int Matches, double? Row, double? Column, double? Score, double? OffsetPx);

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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Shape/NCC native benchmark requires Windows x64.");

        int iterations = ReadPositiveInt(args, "--iterations", 12);
        string algorithm = ReadOption(args, "--algorithm", "both").ToLowerInvariant();
        if (algorithm != "both" && algorithm != "ncc" && algorithm != "shape")
            throw new ArgumentException("--algorithm must be ncc, shape, or both.");
        string output = ReadOption(args, "--output", "artifacts/model-benchmark.json");

        string? templateArg = FindOption(args, "--template");
        string? sceneArg = FindOption(args, "--scene");
        if ((templateArg is null) != (sceneArg is null))
            throw new ArgumentException("--template and --scene must be supplied together.");
        bool synthetic = templateArg is null;

        JLVisionRuntime.Initialize();
        using SyntheticVisionFixture? fixture = synthetic ? new SyntheticVisionFixture() : null;
        string templatePath = templateArg ?? fixture!.TemplatePath;
        string scenePath = sceneArg ?? fixture!.ScenePath;

        // Loading the same image pair outside timed model-creation loops keeps
        // file I/O out of train/match measurements.
        using var template = new JlImage(templatePath);
        using var scene = new JlImage(scenePath);
        var measurements = new List<Measurement>(iterations * 2);
        var memory = new List<MemoryReading>();
        var failures = new List<string>();

        void Snapshot(string phase)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            memory.Add(new MemoryReading(phase, measurements.Count,
                process.PrivateMemorySize64, GC.GetTotalMemory(false)));
        }

        // Warm up creation, matching and Dispose with the same fixture.
        string[] algorithms = algorithm == "both" ? new[] { "ncc", "shape" } : new[] { algorithm };
        foreach (string kind in algorithms)
            Execute(kind, template, scene);
        Snapshot("after-warmup");

        for (int i = 0; i < iterations; i++)
        {
            foreach (string kind in algorithms)
            {
                try
                {
                    measurements.Add(Execute(kind, template, scene));
                }
                catch (Exception e)
                {
                    failures.Add($"iteration={i + 1}, algorithm={kind}: {e}");
                    break;
                }
            }
            if (failures.Count > 0)
                break;
            if ((i + 1) % Math.Max(1, iterations / 5) == 0 || i == iterations - 1)
                Snapshot($"batch-{i + 1}");
        }

        var breakdown = algorithms.Select(name =>
        {
            Measurement[] subset = measurements.Where(m => m.Algorithm == name).ToArray();
            double[] train = subset.Select(m => m.TrainMs).OrderBy(x => x).ToArray();
            double[] match = subset.Select(m => m.MatchMs).OrderBy(x => x).ToArray();
            double[] score = subset.Where(m => m.Score.HasValue).Select(m => m.Score!.Value).ToArray();
            double[] offset = subset.Where(m => m.OffsetPx.HasValue).Select(m => m.OffsetPx!.Value).ToArray();
            return new
            {
                algorithm = name,
                completed = subset.Length,
                detected = subset.Count(m => m.Matches > 0),
                trainMs = new { p50 = Percentile(train, .50), p95 = Percentile(train, .95) },
                matchMs = new { p50 = Percentile(match, .50), p95 = Percentile(match, .95) },
                scoreMean = score.Length == 0 ? (double?)null : score.Average(),
                syntheticOffsetPxMean = offset.Length == 0 ? (double?)null : offset.Average()
            };
        }).ToArray();

        var report = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            synthetic,
            templateFilename = Path.GetFileName(templatePath),
            sceneFilename = Path.GetFileName(scenePath),
            configuration = new { algorithm, iterations },
            results = breakdown,
            memorySamples = memory,
            failures
        };
        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        foreach (var row in breakdown)
            Console.WriteLine($"{row.algorithm}: completed={row.completed}, found={row.detected}, train p95={row.trainMs.p95:F2} ms, match p95={row.matchMs.p95:F2} ms, mean score={row.scoreMean:F3}");
        Console.WriteLine("Report: " + fullPath);

        return failures.Count == 0 && breakdown.All(x => x.completed == iterations && x.detected == iterations) ? 0 : 2;

        Measurement Execute(string kind, JlImage templateImage, JlImage sceneImage)
        {
            long begin = Stopwatch.GetTimestamp();
            if (kind == "ncc")
            {
                using var model = new JlNCCModel(templateImage, 3, -0.10, 0.20, 0.03, "use_polarity");
                long afterTrain = Stopwatch.GetTimestamp();
                model.FindNccModel(sceneImage, -0.10, 0.20, 0.35, 1, 0.5, "true", 0,
                    out JlTuple rows, out JlTuple columns, out JlTuple angles, out JlTuple scores);
                long afterMatch = Stopwatch.GetTimestamp();
                using (rows)
                using (columns)
                using (angles)
                using (scores)
                    return Record(kind, rows, columns, scores, begin, afterTrain, afterMatch);
            }
            else
            {
                using var model = new JlShapeModel(templateImage, 3, -0.10, 0.20, 0.03,
                    "auto", "use_polarity", 20, 10);
                long afterTrain = Stopwatch.GetTimestamp();
                model.FindShapeModel(sceneImage, -0.10, 0.20, 0.35, 1, 0.5, "least_squares", 0, 0.8,
                    out JlTuple rows, out JlTuple columns, out JlTuple angles, out JlTuple scores);
                long afterMatch = Stopwatch.GetTimestamp();
                using (rows)
                using (columns)
                using (angles)
                using (scores)
                    return Record(kind, rows, columns, scores, begin, afterTrain, afterMatch);
            }
        }

        Measurement Record(string kind, JlTuple rows, JlTuple columns, JlTuple scores,
            long begin, long afterTrain, long afterMatch)
        {
            if (rows.Length != columns.Length || rows.Length != scores.Length)
                throw new InvalidOperationException("Native model returned inconsistent match vector lengths.");
            double? row = rows.Length > 0 ? rows[0].D : null;
            double? col = rows.Length > 0 ? columns[0].D : null;
            double? score = rows.Length > 0 ? scores[0].D : null;
            double? offset = null;
            if (synthetic && row.HasValue && col.HasValue)
            {
                double dy = row.Value - (SyntheticVisionFixture.TargetTop + SyntheticVisionFixture.TargetHeight / 2.0);
                double dx = col.Value - (SyntheticVisionFixture.TargetLeft + SyntheticVisionFixture.TargetWidth / 2.0);
                offset = Math.Sqrt(dx * dx + dy * dy);
            }
            double ToMs(long delta) => delta * 1000.0 / Stopwatch.Frequency;
            return new Measurement(kind, ToMs(afterTrain - begin), ToMs(afterMatch - afterTrain),
                rows.Length, row, col, score, offset);
        }
    }

    private static double Percentile(double[] sorted, double value)
        => sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling((sorted.Length - 1) * value)];

    private static int ReadPositiveInt(string[] args, string option, int fallback)
    {
        if (!int.TryParse(ReadOption(args, option, fallback.ToString()), out int value) ||
            value < 1 || value > 20_000)
            throw new ArgumentException($"{option} must be between 1 and 20000.");
        return value;
    }

    private static string ReadOption(string[] args, string name, string fallback)
        => FindOption(args, name) ?? fallback;

    private static string? FindOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name)
            {
                if (i + 1 == args.Length)
                    throw new ArgumentException($"Missing value for {name}");
                return args[i + 1];
            }
        return null;
    }
}

using System.Runtime.InteropServices;
using System.Text.Json;
using JLVisionLib;

// Diagnostic only: query native operator registry labels; never execute operators.
// This does NOT prove that training/recognition can run with a real image/model.
internal static class Program
{
    [DllImport("JLVisionCore", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "HLILogicalName")]
    private static extern IntPtr NativeLogicalName(int index);

    private static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Native registry probe requires Windows x64.");
            string output = Argument(args, "--output") ?? "artifacts/recognition-registry.json";
            int start = ParseIndex(args, "--start", 0);
            int end = ParseIndex(args, "--end", 2174);
            if (start < 0 || end < start || end > 2174)
                throw new ArgumentOutOfRangeException(nameof(args),
                    "Allowed read-only index range is 0..2174 (highest existing wrapper index).");
            JLVisionRuntime.Initialize();
            var matching = new List<object>();
            var anchors = new Dictionary<int, string>();
            int nonempty = 0;
            for (int i = start; i <= end; i++)
            {
                IntPtr pointer = NativeLogicalName(i);
                if (pointer == IntPtr.Zero) continue;
                string? name = Marshal.PtrToStringAnsi(pointer);
                if (string.IsNullOrWhiteSpace(name)) continue;
                nonempty++;
                if (i == 416 || i == 570 || i == 2173)
                    anchors[i] = name;
                if (ContainsRecognitionKeyword(name))
                    matching.Add(new { index = i, operatorName = name });
            }
            string? directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (directory is not null) Directory.CreateDirectory(directory);
            var result = new
            {
                schemaVersion = 1,
                evidence = "Native operator registry name lookup only; not functional validation",
                scanStart = start,
                scanEnd = end,
                nonempty,
                anchors,
                recognitionCandidates = matching,
                operatorExecutionVerified = false,
                restorationReady = false,
                caution = "A returned name is not an ABI/signature/behavior test. Unknown or empty ids are not proof of absence."
            };
            File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Native logical name probe: indices {start}..{end}, nonempty={nonempty}; matches={matching.Count}");
            foreach (var item in matching) Console.WriteLine(JsonSerializer.Serialize(item));
            Console.WriteLine("JSON: " + output);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Native registry probe cannot complete: " + e);
            return 1;
        }
    }

    private static bool ContainsRecognitionKeyword(string name) =>
        name.Contains("bar_code", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("barcode", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("data_code", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("datacode", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ocr", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ocv", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("deep_counting", StringComparison.OrdinalIgnoreCase);

    private static string? Argument(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == key)
                return i + 1 < args.Length ? args[i + 1] :
                    throw new ArgumentException("Missing value for " + key);
        return null;
    }

    private static int ParseIndex(string[] args, string key, int otherwise)
    {
        string? input = Argument(args, key);
        if (input is null) return otherwise;
        return int.TryParse(input, out int value) ? value :
            throw new ArgumentException("Expected integer for " + key);
    }
}

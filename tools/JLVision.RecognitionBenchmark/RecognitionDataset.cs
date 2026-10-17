using JLVisionLib.Recognition;
using System.Text.Json.Serialization;

namespace JLVision.RecognitionBenchmark;

public sealed class RecognitionDataset
{
    public int SchemaVersion { get; set; }
    public string SourceType { get; set; } = "";
    public string DatasetId { get; set; } = "";
    public string SoftwareCommit { get; set; } = "";
    public RecognitionLimits? Acceptance { get; set; }
    public List<RecognitionFrame> Frames { get; set; } = new();
}

public sealed class RecognitionFrame
{
    public string Id { get; set; } = "";
    public string File { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Condition { get; set; } = "";
    public bool? ExpectedPresent { get; set; }
    public string? ExpectedText { get; set; }
    public string? ExpectedFormat { get; set; }
    public JlRecognitionRoi? Roi { get; set; }
}

public sealed class RecognitionLimits
{
    public int MinPositivePerKind { get; set; }
    public int MinNegativePerKind { get; set; }
    public int MinPositivePerCondition { get; set; }
    public int MinNegativePerCondition { get; set; }
    public double MaxMissRate { get; set; }
    public double MaxMisreadRate { get; set; }
    public double MaxFalsePositiveRate { get; set; }
    public double MaxP95Ms { get; set; }
    /// <summary>Optional, stricter gate: compare Wilson 95% upper bounds against configured error-rate limits.</summary>
    public bool RequireWilsonUpper95 { get; set; }
    public List<string> RequiredKinds { get; set; } = new();
    public List<string> RequiredConditions { get; set; } = new();

    public void Validate()
    {
        if (MinPositivePerKind < 1 || MinNegativePerKind < 1 ||
            MinPositivePerCondition < 0 || MinNegativePerCondition < 0)
            throw new InvalidDataException("Positive and negative sample minimums must be valid.");
        foreach (double rate in new[] { MaxMissRate, MaxMisreadRate, MaxFalsePositiveRate })
            if (!double.IsFinite(rate) || rate < 0 || rate > 1)
                throw new InvalidDataException("Error rates must be finite and within [0, 1].");
        if (!double.IsFinite(MaxP95Ms) || MaxP95Ms <= 0)
            throw new InvalidDataException("MaxP95Ms must be finite and positive.");
        if (RequiredKinds is null || RequiredKinds.Count == 0 ||
            RequiredKinds.Distinct(StringComparer.Ordinal).Count() != RequiredKinds.Count ||
            RequiredKinds.Any(kind => !RecognitionEvaluator.AllowedKinds.Contains(kind)))
            throw new InvalidDataException("RequiredKinds must name unique barcode1d, datacode2d or ocr families.");
        if (RequiredConditions is null ||
            RequiredConditions.Distinct(StringComparer.Ordinal).Count() != RequiredConditions.Count ||
            RequiredConditions.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("RequiredConditions must be unique, nonblank labels.");
    }
}

public sealed record RecognitionObservation(
    string Id, string Kind, string Condition, bool ExpectedPresent,
    bool Detected, bool Accepted, bool Misread, double ElapsedMs,
    int ResultCount, string ImageSha256, string? ErrorType = null);

/// <summary>Wilson score 95% interval for a binomial proportion; null bounds mean no denominator.</summary>
public sealed record RecognitionRateInterval(
    int Events, int Trials, double? Estimate, double? Lower95, double? Upper95);

public sealed record RecognitionUncertainty(
    RecognitionRateInterval CorrectRead,
    RecognitionRateInterval Miss,
    RecognitionRateInterval Misread,
    RecognitionRateInterval FalsePositive);

public sealed record RecognitionMetrics(
    int Total, int Positive, int Negative, int TruePositive, int FalseNegative,
    int TrueNegative, int FalsePositive, int Misread, int ProcessingErrors,
    double MissRate, double MisreadRate, double FalsePositiveRate, double P95Ms,
    RecognitionUncertainty Uncertainty);

public sealed record RecognitionBenchmarkReport(
    int SchemaVersion, string DatasetId, string SourceType, string DeclaredSoftwareCommit,
    string ManifestSha256, string Verdict, bool RealFieldAccuracyCertified,
    RecognitionMetrics Overall, Dictionary<string, RecognitionMetrics> ByKind,
    Dictionary<string, RecognitionMetrics> ByCondition,
    Dictionary<string, RecognitionMetrics> ByKindCondition, List<string> Reasons,
    IReadOnlyList<RecognitionObservation> Frames);

public static class RecognitionEvaluator
{
    public static readonly HashSet<string> AllowedKinds =
        new(StringComparer.Ordinal) { "barcode1d", "datacode2d", "ocr" };
    private static readonly HashSet<string> OneDimensionalFormats =
        new(StringComparer.Ordinal) { "CODE_128", "CODE_39", "CODE_93", "EAN_13", "EAN_8", "UPC_A", "UPC_E", "ITF", "CODABAR" };
    private static readonly HashSet<string> TwoDimensionalFormats =
        new(StringComparer.Ordinal) { "QR_CODE", "DATA_MATRIX" };

    public static void Validate(RecognitionDataset dataset)
    {
        if (dataset.SchemaVersion != 1 ||
            dataset.SourceType is not ("field" or "synthetic") ||
            string.IsNullOrWhiteSpace(dataset.DatasetId) ||
            !System.Text.RegularExpressions.Regex.IsMatch(dataset.SoftwareCommit ?? "",
                @"\A[0-9a-fA-F]{40}\z"))
            throw new InvalidDataException("Dataset schema, identity, provenance or declared 40-char commit is invalid.");
        dataset.Acceptance?.Validate();
        if (dataset.Acceptance is null)
            throw new InvalidDataException("Explicit acceptance thresholds are required.");
        if (dataset.Frames is null || dataset.Frames.Count == 0)
            throw new InvalidDataException("At least one labeled frame is required.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frame in dataset.Frames)
        {
            if (frame is null || string.IsNullOrWhiteSpace(frame.Id) ||
                !ids.Add(frame.Id) || string.IsNullOrWhiteSpace(frame.File) ||
                !AllowedKinds.Contains(frame.Kind) || string.IsNullOrWhiteSpace(frame.Condition) ||
                frame.ExpectedPresent is null)
                throw new InvalidDataException("Each frame needs a unique ID, file, kind, condition and explicit presence label.");
            if (frame.ExpectedPresent == true)
            {
                if (string.IsNullOrEmpty(frame.ExpectedText))
                    throw new InvalidDataException("Positive frames require exact expectedText.");
                if (frame.Kind != "ocr" && string.IsNullOrEmpty(frame.ExpectedFormat))
                    throw new InvalidDataException("Barcode positives require expectedFormat.");
                if (frame.Kind == "ocr" && frame.ExpectedFormat is not null)
                    throw new InvalidDataException("OCR uses text, not a barcode format.");
                if (frame.Kind == "barcode1d" && !OneDimensionalFormats.Contains(frame.ExpectedFormat!))
                    throw new InvalidDataException("Invalid 1D code expectedFormat.");
                if (frame.Kind == "datacode2d" && !TwoDimensionalFormats.Contains(frame.ExpectedFormat!))
                    throw new InvalidDataException("Invalid 2D code expectedFormat.");
            }
            else if (frame.ExpectedText is not null || frame.ExpectedFormat is not null)
                throw new InvalidDataException("Negative frames must not declare expectedText or expectedFormat.");
            if (frame.Roi is JlRecognitionRoi roi &&
                (roi.Width <= 0 || roi.Height <= 0 || roi.Column < 0 || roi.Row < 0))
                throw new InvalidDataException("ROI values must be positive/nonnegative.");
        }
        if (dataset.Frames.Any(f => !dataset.Acceptance.RequiredKinds.Contains(f.Kind)))
            throw new InvalidDataException("Every frame family must be named in RequiredKinds.");
    }

    public static RecognitionBenchmarkReport Evaluate(
        RecognitionDataset dataset, IEnumerable<RecognitionObservation> observations,
        string manifestSha256)
    {
        Validate(dataset);
        var values = observations.ToList();
        if (values.Count != dataset.Frames.Count ||
            !values.Select(x => x.Id).Order(StringComparer.Ordinal)
                .SequenceEqual(dataset.Frames.Select(x => x.Id).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Observed frame IDs must match labeled inputs exactly.");
        var labels = dataset.Frames.ToDictionary(x => x.Id, StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!labels.TryGetValue(value.Id, out var frame) ||
                value.Kind != frame.Kind || value.Condition != frame.Condition ||
                value.ExpectedPresent != frame.ExpectedPresent ||
                !double.IsFinite(value.ElapsedMs) || value.ElapsedMs < 0 ||
                value.Accepted && (!value.ExpectedPresent || !value.Detected) ||
                value.Misread && (!value.ExpectedPresent || !value.Detected || value.Accepted) ||
                value.Detected != (value.ResultCount > 0) || value.ResultCount < 0 ||
                value.Accepted && value.ResultCount != 1 ||
                value.ExpectedPresent && value.Detected && !value.Accepted && !value.Misread ||
                value.ErrorType is not null && value.Accepted)
                throw new InvalidDataException("An observation contradicts its immutable ground truth.");
        }

        RecognitionMetrics overall = Metrics(values);
        var byKind = values.GroupBy(x => x.Kind).ToDictionary(x => x.Key, x => Metrics(x), StringComparer.Ordinal);
        var byCondition = values.GroupBy(x => x.Condition).ToDictionary(x => x.Key, x => Metrics(x), StringComparer.Ordinal);
        var byKindCondition = values.GroupBy(x => x.Kind + "|" + x.Condition)
            .ToDictionary(x => x.Key, x => Metrics(x), StringComparer.Ordinal);
        var reasons = new List<string>();
        var limits = dataset.Acceptance!;
        foreach (string kind in limits.RequiredKinds)
        {
            if (!byKind.TryGetValue(kind, out var m))
            {
                reasons.Add("Missing required algorithm family: " + kind);
                continue;
            }
            Gate(m, kind, limits, reasons, limits.MinPositivePerKind, limits.MinNegativePerKind);
        }
        foreach (string condition in limits.RequiredConditions)
        {
            if (!byCondition.TryGetValue(condition, out var m))
            {
                reasons.Add("Missing required condition: " + condition);
                continue;
            }
            Gate(m, "condition " + condition, limits, reasons,
                limits.MinPositivePerCondition, limits.MinNegativePerCondition);
        }
        foreach (string kind in limits.RequiredKinds)
        foreach (string condition in limits.RequiredConditions)
        {
            string key = kind + "|" + condition;
            if (!byKindCondition.TryGetValue(key, out var bucket))
            {
                reasons.Add("Missing required algorithm-condition combination: " + key);
                continue;
            }
            Gate(bucket, key, limits, reasons,
                limits.MinPositivePerCondition, limits.MinNegativePerCondition);
        }
        if (overall.ProcessingErrors > 0)
            reasons.Add("There are decoder errors; software gates fail closed.");
        string verdict = reasons.Count > 0 ? "REVIEW_REQUIRED" :
            dataset.SourceType == "synthetic" ? "SYNTHETIC_SOFTWARE_GATES_PASSED" :
            "SOFTWARE_GATES_PASSED_HUMAN_REVIEW_REQUIRED";
        return new RecognitionBenchmarkReport(1, dataset.DatasetId, dataset.SourceType,
            dataset.SoftwareCommit, manifestSha256, verdict, false, overall,
            byKind, byCondition, byKindCondition, reasons, values);
    }

    private static void Gate(RecognitionMetrics m, string label, RecognitionLimits l,
        List<string> reasons, int minPositive, int minNegative)
    {
        if (m.Positive < minPositive || m.Negative < minNegative)
            reasons.Add($"{label}: insufficient labeled positive/negative samples ({m.Positive}/{m.Negative}; require {minPositive}/{minNegative})");
        if (m.Positive > 0 && m.MissRate > l.MaxMissRate) reasons.Add(label + ": miss rate exceeds limit");
        if (m.Positive > 0 && m.MisreadRate > l.MaxMisreadRate) reasons.Add(label + ": misread rate exceeds limit");
        if (m.Negative > 0 && m.FalsePositiveRate > l.MaxFalsePositiveRate) reasons.Add(label + ": false-positive rate exceeds limit");
        if (l.RequireWilsonUpper95)
        {
            // Enforce a nonempty denominator for every checked bucket; a missing
            // observation group is not a statistically meaningful zero-error rate.
            if (m.Positive == 0 || m.Negative == 0)
                reasons.Add(label + ": Wilson bound requires both positive and negative samples");
            else
            {
                if (m.Uncertainty.Miss.Upper95!.Value > l.MaxMissRate)
                    reasons.Add(label + ": Wilson upper 95% miss rate exceeds limit");
                if (m.Uncertainty.Misread.Upper95!.Value > l.MaxMisreadRate)
                    reasons.Add(label + ": Wilson upper 95% misread rate exceeds limit");
                if (m.Uncertainty.FalsePositive.Upper95!.Value > l.MaxFalsePositiveRate)
                    reasons.Add(label + ": Wilson upper 95% false-positive rate exceeds limit");
            }
        }
        if (m.P95Ms > l.MaxP95Ms) reasons.Add(label + ": P95 latency exceeds limit");
        if (m.ProcessingErrors > 0) reasons.Add(label + ": processing errors found");
    }

    public static RecognitionMetrics Metrics(IEnumerable<RecognitionObservation> source)
    {
        var values = source.ToList();
        int p = values.Count(x => x.ExpectedPresent);
        int n = values.Count - p;
        int tp = values.Count(x => x.ExpectedPresent && x.Accepted);
        int fn = p - tp;
        int fp = values.Count(x => !x.ExpectedPresent && x.Detected);
        int wrong = values.Count(x => x.ExpectedPresent && x.Misread);
        int errors = values.Count(x => x.ErrorType is not null);
        double[] times = values.Select(x => x.ElapsedMs).OrderBy(x => x).ToArray();
        double p95 = times.Length == 0 ? 0 : times[(int)Math.Ceiling(0.95 * times.Length) - 1];
        // Fail closed: if a class has no ground truth, its rate cannot silently look perfect.
        return new RecognitionMetrics(values.Count, p, n, tp, fn, n - fp, fp, wrong, errors,
            p == 0 ? 1 : (double)fn / p,
            p == 0 ? 1 : (double)wrong / p,
            n == 0 ? 1 : (double)fp / n, p95,
            new RecognitionUncertainty(
                WilsonInterval(tp, p), WilsonInterval(fn, p),
                WilsonInterval(wrong, p), WilsonInterval(fp, n)));
    }
    /// <summary>
    /// Two-sided 95% Wilson score interval for independent Bernoulli trials.
    /// This quantifies sampling uncertainty but DOES NOT correct for correlated
    /// frames, mislabeled data, source selection bias or shifting production conditions.
    /// </summary>
    public static RecognitionRateInterval WilsonInterval(int events, int trials)
    {
        if (trials < 0 || events < 0 || events > trials)
            throw new ArgumentOutOfRangeException(nameof(events), "Expected 0 <= events <= trials.");
        if (trials == 0)
            return new RecognitionRateInterval(0, 0, null, null, null);
        const double z = 1.959963984540054;
        double proportion = (double)events / trials;
        double z2 = z * z;
        double divisor = 1 + z2 / trials;
        double center = (proportion + z2 / (2 * trials)) / divisor;
        double half = z / divisor * Math.Sqrt(
            proportion * (1 - proportion) / trials + z2 / (4.0 * trials * trials));
        return new RecognitionRateInterval(events, trials, proportion,
            Math.Max(0, center - half), Math.Min(1, center + half));
    }

}
